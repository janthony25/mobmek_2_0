using Google.Apis.Calendar.v3.Data;
using MobmekApi.Data;
using MobmekApi.Entities;
using MobmekApi.Services;
using MobmekApi.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MobmekApi.Tests.Services;

/// <summary>
/// Covers <see cref="CalendarSyncJob"/>'s push logic per <c>docs/google-calendar-sync-design.md</c>
/// §4 and §8: insert-vs-update selection, success removing the outbox row, failure backoff, and a
/// vanished appointment dropping its row cleanly. Drives <see cref="CalendarSyncJob.PushDueItemsAsync"/>
/// directly rather than running the hosted timer loop.
/// </summary>
public class CalendarSyncJobTests
{
    private static readonly DateTime Start = new(2026, 7, 6, 9, 0, 0, DateTimeKind.Utc);

    private static (AppDbContext Db, CalendarSyncJob Job, CalendarSyncStatus Status) CreateJob(FakeGoogleCalendarClient client)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<IGoogleCalendarClient>(client);
        services.AddLogging();
        var provider = services.BuildServiceProvider();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:BaseUrl"] = "http://localhost:3000" })
            .Build();

        var status = new CalendarSyncStatus();
        var job = new CalendarSyncJob(
            provider.GetRequiredService<IServiceScopeFactory>(),
            configuration,
            status,
            provider.GetRequiredService<ILogger<CalendarSyncJob>>());

        return (db, job, status);
    }

    private static Appointment SeedAppointment(AppDbContext db, string? googleEventId = null)
    {
        var appointment = new Appointment
        {
            Title = "Brake inspection",
            StartUtc = Start,
            EndUtc = Start.AddHours(1),
            Status = AppointmentStatus.Scheduled,
            ContactName = "Dave Miller",
            ContactPhone = "0215551234",
            GoogleEventId = googleEventId,
        };
        db.Appointments.Add(appointment);
        db.SaveChanges();
        return appointment;
    }

    private static CalendarSyncItem SeedUpsertItem(AppDbContext db, Guid appointmentId)
    {
        var item = new CalendarSyncItem { Action = CalendarSyncAction.Upsert, AppointmentId = appointmentId };
        db.CalendarSyncItems.Add(item);
        db.SaveChanges();
        return item;
    }

    [Fact]
    public async Task PushDueItemsAsync_NullEventId_Inserts_AndStoresNewEventId()
    {
        var client = new FakeGoogleCalendarClient { IsConfigured = true };
        var (db, job, _) = CreateJob(client);
        var appointment = SeedAppointment(db);
        SeedUpsertItem(db, appointment.Id);

        await job.PushDueItemsAsync(CancellationToken.None);

        Assert.Single(client.InsertedEvents);
        Assert.Empty(client.UpdatedEvents);
        var updated = await db.Appointments.FirstAsync(a => a.Id == appointment.Id);
        Assert.NotNull(updated.GoogleEventId);
        Assert.Empty(await db.CalendarSyncItems.ToListAsync());
    }

    [Fact]
    public async Task PushDueItemsAsync_LegacyForeignEventId_NotOnWorkshopCalendar_Inserts()
    {
        var client = new FakeGoogleCalendarClient { IsConfigured = true };
        // Deliberately NOT added to client.ExistingEventIds — simulates one of the 230 imported
        // ids pointing at the old personal calendar, not this one.
        var (db, job, _) = CreateJob(client);
        var appointment = SeedAppointment(db, googleEventId: "legacy-personal-calendar-event-id");
        SeedUpsertItem(db, appointment.Id);

        await job.PushDueItemsAsync(CancellationToken.None);

        Assert.Single(client.InsertedEvents);
        Assert.Empty(client.UpdatedEvents);
    }

    [Fact]
    public async Task PushDueItemsAsync_KnownEventIdOnWorkshopCalendar_Updates()
    {
        var client = new FakeGoogleCalendarClient { IsConfigured = true };
        client.ExistingEventIds.Add("workshop-event-1");
        var (db, job, _) = CreateJob(client);
        var appointment = SeedAppointment(db, googleEventId: "workshop-event-1");
        SeedUpsertItem(db, appointment.Id);

        await job.PushDueItemsAsync(CancellationToken.None);

        Assert.Empty(client.InsertedEvents);
        var (eventId, _) = Assert.Single(client.UpdatedEvents);
        Assert.Equal("workshop-event-1", eventId);
        Assert.Empty(await db.CalendarSyncItems.ToListAsync());
    }

    [Fact]
    public async Task PushDueItemsAsync_DeleteAction_CallsDelete_AndRemovesRow()
    {
        var client = new FakeGoogleCalendarClient { IsConfigured = true };
        client.ExistingEventIds.Add("to-delete");
        var (db, job, _) = CreateJob(client);
        db.CalendarSyncItems.Add(new CalendarSyncItem { Action = CalendarSyncAction.Delete, GoogleEventId = "to-delete" });
        await db.SaveChangesAsync();

        await job.PushDueItemsAsync(CancellationToken.None);

        Assert.Equal(["to-delete"], client.DeletedEventIds);
        Assert.Empty(await db.CalendarSyncItems.ToListAsync());
    }

    [Fact]
    public async Task PushDueItemsAsync_VanishedAppointment_DropsRow_WithoutCallingClient()
    {
        var client = new FakeGoogleCalendarClient { IsConfigured = true };
        var (db, job, _) = CreateJob(client);
        // No appointment ever created for this id — e.g. hard-deleted before the job ran.
        SeedUpsertItem(db, Guid.NewGuid());

        await job.PushDueItemsAsync(CancellationToken.None);

        Assert.Empty(client.InsertedEvents);
        Assert.Empty(client.UpdatedEvents);
        Assert.Empty(await db.CalendarSyncItems.ToListAsync());
    }

    [Fact]
    public async Task PushDueItemsAsync_InsertFailure_RecordsErrorAndSchedulesBackoff()
    {
        var client = new FakeGoogleCalendarClient { IsConfigured = true };
        client.EnqueueInsertFailure(new InvalidOperationException("simulated Google API failure"));
        var (db, job, _) = CreateJob(client);
        var appointment = SeedAppointment(db);
        SeedUpsertItem(db, appointment.Id);

        await job.PushDueItemsAsync(CancellationToken.None);

        var row = Assert.Single(await db.CalendarSyncItems.ToListAsync());
        Assert.Equal(1, row.Attempts);
        Assert.Contains("simulated Google API failure", row.LastError);
        Assert.True(row.NextAttemptUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task PushDueItemsAsync_WhenUnconfigured_DoesNothing()
    {
        var client = new FakeGoogleCalendarClient { IsConfigured = false };
        var (db, job, _) = CreateJob(client);
        var appointment = SeedAppointment(db);
        SeedUpsertItem(db, appointment.Id);

        await job.PushDueItemsAsync(CancellationToken.None);

        Assert.Empty(client.InsertedEvents);
        Assert.Single(await db.CalendarSyncItems.ToListAsync());
    }

    [Fact]
    public async Task PushDueItemsAsync_NotYetDue_IsSkipped()
    {
        var client = new FakeGoogleCalendarClient { IsConfigured = true };
        var (db, job, _) = CreateJob(client);
        var appointment = SeedAppointment(db);
        var item = SeedUpsertItem(db, appointment.Id);
        item.NextAttemptUtc = DateTime.UtcNow.AddMinutes(5);
        await db.SaveChangesAsync();

        await job.PushDueItemsAsync(CancellationToken.None);

        Assert.Empty(client.InsertedEvents);
        Assert.Single(await db.CalendarSyncItems.ToListAsync());
    }

    // --- Reconcile & backfill (design doc §6, §8) ---

    private static readonly DateTime FutureStart = DateTime.UtcNow.Date.AddDays(2).AddHours(9);
    private static readonly DateTime PastStart = DateTime.UtcNow.Date.AddDays(-2).AddHours(9);

    [Fact]
    public async Task ReconcileAsync_EventWithNoMatchingAppointment_IsOrphan_EnqueuesDelete()
    {
        var client = new FakeGoogleCalendarClient { IsConfigured = true };
        client.CalendarEvents.Add(new Event { Id = "hand-created-event", Summary = "Not ours" });
        var (db, job, _) = CreateJob(client);

        await job.ReconcileAsync(backfill: false, CancellationToken.None);

        var row = Assert.Single(await db.CalendarSyncItems.ToListAsync());
        Assert.Equal(CalendarSyncAction.Delete, row.Action);
        Assert.Equal("hand-created-event", row.GoogleEventId);
    }

    [Fact]
    public async Task ReconcileAsync_FutureAppointmentMissingItsEvent_ReEnqueuesUpsert()
    {
        var client = new FakeGoogleCalendarClient { IsConfigured = true };
        var (db, job, _) = CreateJob(client);
        var appointment = SeedFutureAppointment(db, googleEventId: "not-on-calendar-anymore");

        await job.ReconcileAsync(backfill: false, CancellationToken.None);

        var row = Assert.Single(await db.CalendarSyncItems.ToListAsync());
        Assert.Equal(CalendarSyncAction.Upsert, row.Action);
        Assert.Equal(appointment.Id, row.AppointmentId);
    }

    [Fact]
    public async Task ReconcileAsync_EventDriftedFromMappedContent_ReEnqueuesUpsert()
    {
        var client = new FakeGoogleCalendarClient { IsConfigured = true };
        var (db, job, _) = CreateJob(client);
        var appointment = SeedFutureAppointment(db, googleEventId: "evt-1");
        client.CalendarEvents.Add(new Event
        {
            Id = "evt-1",
            Summary = "Someone edited this on the GCal side",
            ColorId = "1",
        });

        await job.ReconcileAsync(backfill: false, CancellationToken.None);

        var row = Assert.Single(await db.CalendarSyncItems.ToListAsync());
        Assert.Equal(CalendarSyncAction.Upsert, row.Action);
        Assert.Equal(appointment.Id, row.AppointmentId);
    }

    [Fact]
    public async Task ReconcileAsync_EventMatchesMappedContentExactly_EnqueuesNothing()
    {
        var client = new FakeGoogleCalendarClient { IsConfigured = true };
        var (db, job, _) = CreateJob(client);
        var appointment = SeedFutureAppointment(db, googleEventId: "evt-1");
        var expected = CalendarEventMapper.Map(appointment, "http://localhost:3000");
        client.CalendarEvents.Add(new Event
        {
            Id = "evt-1",
            Summary = expected.Summary,
            Description = expected.Description,
            ColorId = expected.ColorId,
            Start = expected.Start,
            End = expected.End,
        });

        await job.ReconcileAsync(backfill: false, CancellationToken.None);

        Assert.Empty(await db.CalendarSyncItems.ToListAsync());
    }

    [Fact]
    public async Task ReconcileAsync_PastAppointment_IsIgnoredEntirely()
    {
        var client = new FakeGoogleCalendarClient { IsConfigured = true };
        var (db, job, _) = CreateJob(client);
        SeedAppointmentAt(db, PastStart, googleEventId: null);

        await job.ReconcileAsync(backfill: false, CancellationToken.None);

        Assert.Empty(await db.CalendarSyncItems.ToListAsync());
    }

    [Fact]
    public async Task ReconcileAsync_RecordsSuccessOnStatus()
    {
        var client = new FakeGoogleCalendarClient { IsConfigured = true };
        var (_, job, status) = CreateJob(client);

        await job.ReconcileAsync(backfill: false, CancellationToken.None);

        var (lastReconcileUtc, lastReconcileError) = status.Snapshot();
        Assert.NotNull(lastReconcileUtc);
        Assert.Null(lastReconcileError);
    }

    [Fact]
    public async Task BackfillAsync_EnqueuesUpsertForFutureNonCancelledAppointments_Only()
    {
        var client = new FakeGoogleCalendarClient { IsConfigured = true };
        var (db, job, _) = CreateJob(client);
        var future = SeedFutureAppointment(db);
        SeedAppointmentAt(db, FutureStart, status: AppointmentStatus.Cancelled);
        SeedAppointmentAt(db, PastStart);

        await job.ReconcileAsync(backfill: true, CancellationToken.None);

        var row = Assert.Single(await db.CalendarSyncItems.Where(i => i.Action == CalendarSyncAction.Upsert).ToListAsync());
        Assert.Equal(future.Id, row.AppointmentId);
    }

    [Fact]
    public async Task BackfillAsync_SkipsAppointmentWithAlreadyPendingUpsert()
    {
        var client = new FakeGoogleCalendarClient { IsConfigured = true };
        var (db, job, _) = CreateJob(client);
        var appointment = SeedFutureAppointment(db);
        SeedUpsertItem(db, appointment.Id);

        await job.ReconcileAsync(backfill: true, CancellationToken.None);

        Assert.Single(await db.CalendarSyncItems.Where(i => i.AppointmentId == appointment.Id).ToListAsync());
    }

    private static Appointment SeedFutureAppointment(AppDbContext db, string? googleEventId = null) =>
        SeedAppointmentAt(db, FutureStart, googleEventId);

    private static Appointment SeedAppointmentAt(
        AppDbContext db, DateTime startUtc, string? googleEventId = null, AppointmentStatus status = AppointmentStatus.Scheduled)
    {
        var appointment = new Appointment
        {
            Title = "Brake inspection",
            StartUtc = startUtc,
            EndUtc = startUtc.AddHours(1),
            Status = status,
            ContactName = "Dave Miller",
            ContactPhone = "0215551234",
            GoogleEventId = googleEventId,
        };
        db.Appointments.Add(appointment);
        db.SaveChanges();
        return appointment;
    }
}
