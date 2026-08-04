using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using MobmekApi.Tests.Fakes;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

/// <summary>
/// Covers the Google Calendar sync enqueue hooks in <see cref="AppointmentService"/>: a
/// create/update/delete writes the right <see cref="CalendarSyncItem"/> outbox row (or none, when
/// unconfigured), and re-editing before the job runs coalesces into a single pending row — per
/// <c>docs/google-calendar-sync-design.md</c> §4 and §8. The actual push to Google is
/// <see cref="CalendarSyncJobTests"/>; this file only checks what lands in the outbox.
/// </summary>
public class AppointmentCalendarSyncTests
{
    private static readonly DateTime Start = new(2026, 7, 6, 9, 0, 0, DateTimeKind.Utc);

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static CreateAppointmentRequest NewCallerBooking() =>
        new(
            "Brake inspection",
            Start,
            Start.AddHours(1),
            AppointmentStatus.Scheduled,
            null,
            "Dave Miller",
            "0215551234",
            "White 2014 Hilux",
            null, null, null, null);

    [Fact]
    public async Task CreateAsync_WhenConfigured_EnqueuesUpsertRow()
    {
        await using var db = CreateContext();
        var client = new FakeGoogleCalendarClient { IsConfigured = true };
        var service = new AppointmentService(db, client);

        var (appointment, _) = await service.CreateAsync(NewCallerBooking());

        var rows = await db.CalendarSyncItems.ToListAsync();
        var row = Assert.Single(rows);
        Assert.Equal(CalendarSyncAction.Upsert, row.Action);
        Assert.Equal(appointment!.Id, row.AppointmentId);
    }

    [Fact]
    public async Task CreateAsync_WhenUnconfigured_EnqueuesNothing()
    {
        await using var db = CreateContext();
        var client = new FakeGoogleCalendarClient { IsConfigured = false };
        var service = new AppointmentService(db, client);

        await service.CreateAsync(NewCallerBooking());

        Assert.Empty(await db.CalendarSyncItems.ToListAsync());
    }

    [Fact]
    public async Task UpdateAsync_WhenNoPendingRow_EnqueuesNewUpsertRow()
    {
        await using var db = CreateContext();
        var client = new FakeGoogleCalendarClient { IsConfigured = true };
        var service = new AppointmentService(db, client);
        var (appointment, _) = await service.CreateAsync(NewCallerBooking());

        // Simulate the sync job already having pushed and cleared the create's outbox row.
        db.CalendarSyncItems.RemoveRange(db.CalendarSyncItems);
        await db.SaveChangesAsync();

        await service.UpdateAsync(appointment!.Id, ToUpdateRequest(appointment) with { Title = "Brake job" });

        var rows = await db.CalendarSyncItems.ToListAsync();
        var row = Assert.Single(rows);
        Assert.Equal(CalendarSyncAction.Upsert, row.Action);
        Assert.Equal(appointment.Id, row.AppointmentId);
    }

    [Fact]
    public async Task UpdateAsync_EditedTwiceBeforePush_CoalescesIntoOnePendingRow()
    {
        await using var db = CreateContext();
        var client = new FakeGoogleCalendarClient { IsConfigured = true };
        var service = new AppointmentService(db, client);
        var (appointment, _) = await service.CreateAsync(NewCallerBooking());

        await service.UpdateAsync(appointment!.Id, ToUpdateRequest(appointment) with { Title = "First edit" });
        await service.UpdateAsync(appointment.Id, ToUpdateRequest(appointment) with { Title = "Second edit" });

        var rows = await db.CalendarSyncItems.ToListAsync();
        var row = Assert.Single(rows);
        Assert.Equal(appointment.Id, row.AppointmentId);
    }

    [Fact]
    public async Task DeleteAsync_WithSyncedEvent_EnqueuesDeleteRow_AndDropsPendingUpsert()
    {
        await using var db = CreateContext();
        var client = new FakeGoogleCalendarClient { IsConfigured = true };
        var service = new AppointmentService(db, client);
        var (appointment, _) = await service.CreateAsync(NewCallerBooking());

        // Simulate: already pushed once (has a GoogleEventId), then edited again (pending Upsert).
        var entity = await db.Appointments.FirstAsync(a => a.Id == appointment!.Id);
        entity.GoogleEventId = "legacy-personal-calendar-event-id";
        await db.SaveChangesAsync();

        var deleted = await service.DeleteAsync(appointment!.Id);

        Assert.True(deleted);
        var rows = await db.CalendarSyncItems.ToListAsync();
        var row = Assert.Single(rows);
        Assert.Equal(CalendarSyncAction.Delete, row.Action);
        Assert.Equal("legacy-personal-calendar-event-id", row.GoogleEventId);
        Assert.Null(row.AppointmentId);
    }

    [Fact]
    public async Task DeleteAsync_NeverSynced_EnqueuesNoDeleteRow()
    {
        await using var db = CreateContext();
        var client = new FakeGoogleCalendarClient { IsConfigured = true };
        var service = new AppointmentService(db, client);
        var (appointment, _) = await service.CreateAsync(NewCallerBooking());

        await service.DeleteAsync(appointment!.Id);

        // The create's pending Upsert is dropped, and there's no GoogleEventId to snapshot —
        // nothing exists on the calendar yet, so there's nothing to enqueue a delete for.
        Assert.Empty(await db.CalendarSyncItems.ToListAsync());
    }

    [Fact]
    public async Task DeleteAsync_WhenUnconfigured_EnqueuesNothing()
    {
        await using var db = CreateContext();
        var client = new FakeGoogleCalendarClient { IsConfigured = false };
        var service = new AppointmentService(db, client);
        var (appointment, _) = await service.CreateAsync(NewCallerBooking());

        await service.DeleteAsync(appointment!.Id);

        Assert.Empty(await db.CalendarSyncItems.ToListAsync());
    }

    private static UpdateAppointmentRequest ToUpdateRequest(AppointmentDto dto) => new(
        dto.Title, dto.StartUtc, dto.EndUtc, dto.Status, dto.Notes,
        dto.ContactName, dto.ContactPhone, dto.VehicleDescription,
        dto.CustomerId, dto.CarId, dto.JobId, dto.MechanicId);
}
