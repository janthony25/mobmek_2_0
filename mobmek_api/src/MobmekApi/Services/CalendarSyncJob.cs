using Google.Apis.Calendar.v3.Data;
using MobmekApi.Data;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

/// <summary>
/// Pushes due <see cref="CalendarSyncItem"/> outbox rows to Google Calendar every 30 seconds
/// (pattern: <see cref="OutboundStatusPollJob"/>/<see cref="RecurringTransactionPostingJob"/>),
/// and reconciles the Workshop calendar against Postgres every hour (<see cref="ReconcileAsync"/>,
/// also callable on demand — <c>docs/google-calendar-sync-design.md</c> §6). A no-op tick when
/// <see cref="IGoogleCalendarClient.IsConfigured"/> is false — the same graceful-degradation
/// shape as the rest of the sync (design principle 2). Success removes the outbox row; failure
/// records the error and backs off (1 min → 5 min → 30 min → hourly), logging at error level once
/// a row has been failing for 24h+ (a stuck outbox is a config problem, not a transient one).
/// </summary>
public class CalendarSyncJob(
    IServiceScopeFactory scopeFactory, IConfiguration configuration, CalendarSyncStatus status, ILogger<CalendarSyncJob> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReconcileInterval = TimeSpan.FromHours(1);
    private const int BatchSize = 20;

    private DateTime? _lastReconcileAttemptUtc;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        await PushDueItemsAsync(stoppingToken);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PushDueItemsAsync(stoppingToken);
            await ReconcileIfDueAsync(stoppingToken);
        }
    }

    private async Task ReconcileIfDueAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        if (_lastReconcileAttemptUtc is { } last && now - last < ReconcileInterval)
        {
            return;
        }

        _lastReconcileAttemptUtc = now;
        await ReconcileAsync(backfill: false, cancellationToken);
    }

    /// <summary>One sweep of due outbox rows. Public (rather than the usual private tick method)
    /// so tests can drive it directly instead of running the hosted timer loop.</summary>
    public async Task PushDueItemsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var client = scope.ServiceProvider.GetRequiredService<IGoogleCalendarClient>();

            if (!client.IsConfigured)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var dueItems = await db.CalendarSyncItems
                .Where(i => i.NextAttemptUtc <= now)
                .OrderBy(i => i.NextAttemptUtc)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            foreach (var item in dueItems)
            {
                await ProcessItemAsync(db, client, item, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Calendar sync push sweep failed");
        }
    }

    private async Task ProcessItemAsync(AppDbContext db, IGoogleCalendarClient client, CalendarSyncItem item, CancellationToken cancellationToken)
    {
        try
        {
            if (item.Action == CalendarSyncAction.Delete)
            {
                await client.DeleteAsync(item.GoogleEventId!, cancellationToken);
            }
            else
            {
                var appointment = await db.Appointments
                    .Include(a => a.Customer)
                    .Include(a => a.Car).ThenInclude(c => c!.CarMake)
                    .Include(a => a.Car).ThenInclude(c => c!.CarModel)
                    .Include(a => a.Mechanic)
                    .FirstOrDefaultAsync(a => a.Id == item.AppointmentId, cancellationToken);

                if (appointment is null)
                {
                    // The appointment was hard-deleted after this row was enqueued (and before
                    // any delete-row snapshot could apply, since that path removes this row too).
                    db.CalendarSyncItems.Remove(item);
                    await db.SaveChangesAsync(cancellationToken);
                    return;
                }

                var frontendBaseUrl = configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
                var googleEvent = CalendarEventMapper.Map(appointment, frontendBaseUrl);

                // Insert when there's no id yet, or the id doesn't refer to an event on *this*
                // calendar — covers the 230 legacy ids, which point at the old personal calendar.
                var existsOnWorkshopCalendar = appointment.GoogleEventId is not null
                    && await client.EventExistsAsync(appointment.GoogleEventId, cancellationToken);

                if (existsOnWorkshopCalendar)
                {
                    await client.UpdateAsync(appointment.GoogleEventId!, googleEvent, cancellationToken);
                }
                else
                {
                    appointment.GoogleEventId = await client.InsertAsync(googleEvent, cancellationToken);
                }
            }

            db.CalendarSyncItems.Remove(item);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            item.Attempts++;
            item.LastError = ex.Message;
            item.NextAttemptUtc = DateTime.UtcNow + BackoffFor(item.Attempts);

            var failingSince = DateTime.UtcNow - item.CreatedAtUtc;
            if (failingSince > TimeSpan.FromHours(24))
            {
                logger.LogError(ex,
                    "Calendar sync item {ItemId} (appointment {AppointmentId}) has been failing for {Elapsed} — likely a config problem",
                    item.Id, item.AppointmentId, failingSince);
            }
            else
            {
                logger.LogWarning(ex,
                    "Calendar sync item {ItemId} (appointment {AppointmentId}) failed, attempt {Attempts}",
                    item.Id, item.AppointmentId, item.Attempts);
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static TimeSpan BackoffFor(int attempts) => attempts switch
    {
        1 => TimeSpan.FromMinutes(1),
        2 => TimeSpan.FromMinutes(5),
        3 => TimeSpan.FromMinutes(30),
        _ => TimeSpan.FromHours(1),
    };

    /// <summary>
    /// One-time catch-up (first enable) plus the recurring reconcile pass, both per §6. Never
    /// throws — a failure is recorded on <see cref="CalendarSyncStatus"/> and logged, since this
    /// runs unattended from the hourly tick as often as it runs from the admin endpoint.
    /// </summary>
    public async Task ReconcileAsync(bool backfill, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = scope.ServiceProvider.GetRequiredService<IGoogleCalendarClient>();

        if (!client.IsConfigured)
        {
            return;
        }

        try
        {
            if (backfill)
            {
                await BackfillAsync(db, cancellationToken);
            }

            await ReconcileCoreAsync(db, client, cancellationToken);
            status.RecordSuccess(DateTime.UtcNow);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            status.RecordFailure(DateTime.UtcNow, ex.Message);
            logger.LogError(ex, "Calendar reconcile failed");
        }
    }

    /// <summary>Enqueues an Upsert for every future, non-cancelled appointment that doesn't
    /// already have one pending — the one-time catch-up for appointments that predate the sync
    /// being enabled. Cancelled appointments are skipped here specifically: there's no value in
    /// resurrecting an old, already-known cancellation the moment the feature switches on.</summary>
    private async Task BackfillAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var candidates = await db.Appointments
            .Where(a => a.StartUtc >= now && a.Status != AppointmentStatus.Cancelled)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        var alreadyPending = (await db.CalendarSyncItems
                .Where(i => i.Action == CalendarSyncAction.Upsert && i.AppointmentId != null)
                .Select(i => i.AppointmentId!.Value)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var appointmentId in candidates)
        {
            if (alreadyPending.Add(appointmentId))
            {
                db.CalendarSyncItems.Add(new CalendarSyncItem { Action = CalendarSyncAction.Upsert, AppointmentId = appointmentId });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Lists Workshop-calendar events from today forward and compares against Postgres: an event
    /// with no matching appointment is an orphan (deleted); a future appointment missing its event
    /// is re-enqueued; an event that's drifted from what <see cref="CalendarEventMapper"/> would
    /// produce is re-enqueued (the app is always the source of truth). Past events/appointments
    /// are ignored entirely — the Workshop calendar starts at go-live, history lives in the app.
    /// </summary>
    private async Task ReconcileCoreAsync(AppDbContext db, IGoogleCalendarClient client, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;

        var appointments = await db.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Car).ThenInclude(c => c!.CarMake)
            .Include(a => a.Car).ThenInclude(c => c!.CarModel)
            .Include(a => a.Mechanic)
            .Where(a => a.StartUtc >= today)
            .ToListAsync(cancellationToken);

        var calendarEvents = await client.ListUpcomingAsync(today, cancellationToken);
        var eventsById = calendarEvents.ToDictionary(e => e.Id);

        var frontendBaseUrl = configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
        var matchedEventIds = new HashSet<string>();

        foreach (var appointment in appointments)
        {
            var expected = CalendarEventMapper.Map(appointment, frontendBaseUrl);
            var actual = appointment.GoogleEventId is not null ? eventsById.GetValueOrDefault(appointment.GoogleEventId) : null;

            // A cancelled appointment that's never been synced is left alone here too — same
            // reasoning as the backfill skip: no value in creating a calendar presence for an
            // old, already-known cancellation just because reconcile happened to look at it.
            // One that *was* synced and then vanished (hand-deleted from the calendar) still
            // gets recreated below — the app remains the source of truth for anything it once pushed.
            var neverSyncedAndCancelled = appointment.GoogleEventId is null && appointment.Status == AppointmentStatus.Cancelled;

            if (actual is null)
            {
                if (!neverSyncedAndCancelled)
                {
                    await EnqueueUpsertIfNotPendingAsync(db, appointment.Id, cancellationToken);
                }
            }
            else
            {
                matchedEventIds.Add(actual.Id);
                if (!EventsMatch(actual, expected))
                {
                    await EnqueueUpsertIfNotPendingAsync(db, appointment.Id, cancellationToken);
                }
            }
        }

        foreach (var orphanId in eventsById.Keys.Except(matchedEventIds))
        {
            var alreadyQueued = await db.CalendarSyncItems.AnyAsync(
                i => i.Action == CalendarSyncAction.Delete && i.GoogleEventId == orphanId, cancellationToken);
            if (!alreadyQueued)
            {
                db.CalendarSyncItems.Add(new CalendarSyncItem { Action = CalendarSyncAction.Delete, GoogleEventId = orphanId });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnqueueUpsertIfNotPendingAsync(AppDbContext db, Guid appointmentId, CancellationToken cancellationToken)
    {
        var alreadyPending = await db.CalendarSyncItems.AnyAsync(
            i => i.AppointmentId == appointmentId && i.Action == CalendarSyncAction.Upsert, cancellationToken);
        if (!alreadyPending)
        {
            db.CalendarSyncItems.Add(new CalendarSyncItem { Action = CalendarSyncAction.Upsert, AppointmentId = appointmentId });
        }
    }

    private static bool EventsMatch(Event actual, Event expected) =>
        actual.Summary == expected.Summary
        && actual.Description == expected.Description
        && actual.ColorId == expected.ColorId
        && actual.Start?.DateTimeDateTimeOffset == expected.Start?.DateTimeDateTimeOffset
        && actual.End?.DateTimeDateTimeOffset == expected.End?.DateTimeDateTimeOffset;
}
