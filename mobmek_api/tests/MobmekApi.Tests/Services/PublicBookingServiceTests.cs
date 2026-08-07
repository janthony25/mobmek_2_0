using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using MobmekApi.Tests.Fakes;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class PublicBookingServiceTests
{
    // UTC instants are hard-coded rather than derived with TimeZoneInfo, so the tests check the
    // conversion instead of repeating it. New Zealand runs UTC+12 (NZST) in July and UTC+13
    // (NZDT) in January; 2026-07-06 is a Monday, 2026-07-11 a Saturday, 2026-07-12 a Sunday.
    private static readonly DateOnly Monday = new(2026, 7, 6);
    private static readonly DateOnly Saturday = new(2026, 7, 11);
    private static readonly DateOnly Sunday = new(2026, 7, 12);

    /// <summary>Monday 08:30 NZST — the first slot of that day.</summary>
    private static readonly DateTime MondayAt0830Utc = new(2026, 7, 5, 20, 30, 0, DateTimeKind.Utc);

    /// <summary>Monday 09:00 NZST.</summary>
    private static readonly DateTime MondayAt0900Utc = new(2026, 7, 5, 21, 0, 0, DateTimeKind.Utc);

    /// <summary>Monday 15:30 NZST — the last slot, ending at 16:30 close.</summary>
    private static readonly DateTime MondayAt1530Utc = new(2026, 7, 6, 3, 30, 0, DateTimeKind.Utc);

    /// <summary>Well before the Monday under test, so every slot that day is still in the future.</summary>
    private static readonly DateTimeOffset NowBeforeMonday =
        new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>A clock the service can be pinned to — the slot grid is meaningless without one.</summary>
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static PublicBookingService CreateService(
        AppDbContext db,
        DateTimeOffset? now = null,
        IAppointmentChangeNotifier? changeNotifier = null,
        IGoogleCalendarClient? calendarClient = null) =>
        new(
            db,
            new FixedClock(now ?? NowBeforeMonday),
            changeNotifier ?? new FakeAppointmentChangeNotifier(),
            calendarClient ?? new FakeGoogleCalendarClient { IsConfigured = false });

    private static async Task SeedAppointmentAsync(
        AppDbContext db, DateTime startUtc, AppointmentStatus status = AppointmentStatus.Scheduled)
    {
        db.Appointments.Add(new Appointment
        {
            Title = "Existing job",
            StartUtc = startUtc,
            EndUtc = startUtc.AddHours(1),
            Status = status,
            ContactName = "Someone",
            ContactPhone = "021",
        });
        await db.SaveChangesAsync();
    }

    private static CreateBookingRequest BookingAt(DateTime startUtc) =>
        new(
            startUtc,
            "Dave Miller",
            "0215551234",
            "dave@example.com",
            "White 2014 Hilux",
            "Brake inspection",
            null);

    private static BookingDayDto DayFor(AvailabilityDto availability, DateOnly date) =>
        availability.Days.Single(d => d.Date == date);

    private static bool IsAvailableAt(AvailabilityDto availability, DateTime startUtc) =>
        availability.Days.SelectMany(d => d.Slots).Single(s => s.StartUtc == startUtc).Available;

    [Fact]
    public async Task GetAvailability_BuildsFifteenSlots_From0830To1530()
    {
        await using var db = CreateContext();

        var availability = await CreateService(db).GetAvailabilityAsync(Monday, Monday);

        var day = DayFor(availability, Monday);
        Assert.True(day.Open);
        Assert.Equal(15, day.Slots.Count);
        Assert.Equal(MondayAt0830Utc, day.Slots[0].StartUtc);
        Assert.Equal("8:30 AM", day.Slots[0].StartLocal);
        Assert.Equal(MondayAt1530Utc, day.Slots[^1].StartUtc);
        Assert.Equal("3:30 PM", day.Slots[^1].StartLocal);
    }

    [Fact]
    public async Task GetAvailability_ClosesSunday_ButOpensSaturday()
    {
        await using var db = CreateContext();

        var availability = await CreateService(db).GetAvailabilityAsync(Saturday, Sunday);

        Assert.True(DayFor(availability, Saturday).Open);
        Assert.NotEmpty(DayFor(availability, Saturday).Slots);
        Assert.False(DayFor(availability, Sunday).Open);
        Assert.Empty(DayFor(availability, Sunday).Slots);
    }

    [Fact]
    public async Task GetAvailability_HourLongBooking_AlsoBlocksTheHalfHourEitherSide()
    {
        await using var db = CreateContext();
        await SeedAppointmentAsync(db, MondayAt0900Utc);

        var availability = await CreateService(db).GetAvailabilityAsync(Monday, Monday);

        // 09:00-10:00 is taken. 08:30 and 09:30 each run an hour, so both overlap it.
        Assert.False(IsAvailableAt(availability, MondayAt0830Utc));
        Assert.False(IsAvailableAt(availability, MondayAt0900Utc));
        Assert.False(IsAvailableAt(availability, MondayAt0900Utc.AddMinutes(30)));

        // 10:00 merely touches the end of it, so it stays bookable.
        Assert.True(IsAvailableAt(availability, MondayAt0900Utc.AddHours(1)));
    }

    [Theory]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public async Task GetAvailability_CancelledAndNoShow_FreeTheSlotBackUp(AppointmentStatus status)
    {
        await using var db = CreateContext();
        await SeedAppointmentAsync(db, MondayAt0900Utc, status);

        var availability = await CreateService(db).GetAvailabilityAsync(Monday, Monday);

        Assert.True(IsAvailableAt(availability, MondayAt0900Utc));
    }

    [Theory]
    [InlineData(AppointmentStatus.Requested)]
    [InlineData(AppointmentStatus.Confirmed)]
    public async Task GetAvailability_RequestedAndConfirmed_HoldTheSlot(AppointmentStatus status)
    {
        await using var db = CreateContext();
        await SeedAppointmentAsync(db, MondayAt0900Utc, status);

        var availability = await CreateService(db).GetAvailabilityAsync(Monday, Monday);

        Assert.False(IsAvailableAt(availability, MondayAt0900Utc));
    }

    [Fact]
    public async Task GetAvailability_HidesSlotsAlreadyStartedToday()
    {
        await using var db = CreateContext();
        // 10:00 NZST on the Monday: the 08:30 and 09:00 slots have been and gone.
        var now = new DateTimeOffset(2026, 7, 5, 22, 0, 0, TimeSpan.Zero);

        var availability = await CreateService(db, now).GetAvailabilityAsync(Monday, Monday);

        Assert.False(IsAvailableAt(availability, MondayAt0830Utc));
        Assert.False(IsAvailableAt(availability, MondayAt0900Utc));
        Assert.True(IsAvailableAt(availability, MondayAt1530Utc));
    }

    [Fact]
    public async Task GetAvailability_ShiftsWithDaylightSaving()
    {
        await using var db = CreateContext();
        var januaryMonday = new DateOnly(2026, 1, 5);
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var summer = await CreateService(db, now).GetAvailabilityAsync(januaryMonday, januaryMonday);
        var winter = await CreateService(db).GetAvailabilityAsync(Monday, Monday);

        // Same 8:30 on the workshop wall clock, an hour apart in UTC: NZDT (+13) vs NZST (+12).
        Assert.Equal(
            new DateTime(2026, 1, 4, 19, 30, 0, DateTimeKind.Utc),
            DayFor(summer, januaryMonday).Slots[0].StartUtc);
        Assert.Equal(MondayAt0830Utc, DayFor(winter, Monday).Slots[0].StartUtc);
        Assert.Equal("8:30 AM", DayFor(summer, januaryMonday).Slots[0].StartLocal);
    }

    [Fact]
    public async Task GetAvailability_ClampsRequestedRangeToTheBookingHorizon()
    {
        await using var db = CreateContext();
        var today = new DateOnly(2026, 7, 1);

        var availability = await CreateService(db)
            .GetAvailabilityAsync(today.AddDays(-30), today.AddDays(400));

        Assert.Equal(today, availability.FromDate);
        Assert.True(availability.ToDate <= today.AddDays(PublicBookingService.BookingHorizonDays));
        Assert.True(availability.Days.Count <= PublicBookingService.MaxRangeDays);
    }

    [Fact]
    public async Task CreateBooking_PersistsAppointmentAwaitingApproval()
    {
        await using var db = CreateContext();
        var notifier = new FakeAppointmentChangeNotifier();
        var service = CreateService(db, changeNotifier: notifier);

        var (booking, error) = await service.CreateBookingAsync(BookingAt(MondayAt0900Utc));

        Assert.Equal(BookingError.None, error);
        Assert.Equal(MondayAt0900Utc, booking!.StartUtc);
        Assert.Equal(MondayAt0900Utc.AddHours(1), booking.EndUtc);
        Assert.Equal("9:00 AM", booking.StartLocal);

        var saved = await db.Appointments.SingleAsync();
        Assert.Equal(AppointmentStatus.Requested, saved.Status);
        Assert.Equal("Brake inspection", saved.Title);
        Assert.Equal("dave@example.com", saved.ContactEmail);
        Assert.Equal("White 2014 Hilux", saved.VehicleDescription);

        // The staff calendar's live-refresh feature depends on this firing — a booking that
        // doesn't signal the notifier would silently sit unseen until the admin's next
        // manual reload.
        Assert.Equal(1, notifier.NotifyCount);
    }

    [Fact]
    public async Task CreateBooking_WhenConfigured_EnqueuesCalendarUpsertRow()
    {
        await using var db = CreateContext();
        var client = new FakeGoogleCalendarClient { IsConfigured = true };
        var service = CreateService(db, calendarClient: client);

        var (booking, error) = await service.CreateBookingAsync(BookingAt(MondayAt0900Utc));

        Assert.Equal(BookingError.None, error);
        var item = await db.CalendarSyncItems.SingleAsync();
        Assert.Equal(CalendarSyncAction.Upsert, item.Action);
        Assert.Equal(booking!.ReferenceId, item.AppointmentId);
    }

    [Fact]
    public async Task CreateBooking_WhenNotConfigured_DoesNotEnqueueCalendarUpsertRow()
    {
        await using var db = CreateContext();
        var client = new FakeGoogleCalendarClient { IsConfigured = false };
        var service = CreateService(db, calendarClient: client);

        var (_, error) = await service.CreateBookingAsync(BookingAt(MondayAt0900Utc));

        Assert.Equal(BookingError.None, error);
        Assert.Empty(await db.CalendarSyncItems.ToListAsync());
    }

    [Fact]
    public async Task CreateBooking_RejectedBooking_DoesNotNotify()
    {
        await using var db = CreateContext();
        var notifier = new FakeAppointmentChangeNotifier();
        var service = CreateService(db, changeNotifier: notifier);

        var (_, error) = await service.CreateBookingAsync(BookingAt(MondayAt0900Utc.AddMinutes(15)));

        Assert.Equal(BookingError.NotASlotStart, error);
        Assert.Equal(0, notifier.NotifyCount);
    }

    [Fact]
    public async Task CreateBooking_BlocksTheSlotForTheNextCustomer()
    {
        await using var db = CreateContext();
        var service = CreateService(db);
        await service.CreateBookingAsync(BookingAt(MondayAt0900Utc));

        var (_, error) = await service.CreateBookingAsync(BookingAt(MondayAt0900Utc));

        Assert.Equal(BookingError.SlotTaken, error);
        Assert.Equal(1, await db.Appointments.CountAsync());
    }

    [Fact]
    public async Task CreateBooking_RejectsSlotOverlappingAnExistingAppointment()
    {
        await using var db = CreateContext();
        await SeedAppointmentAsync(db, MondayAt0900Utc);

        // 08:30 doesn't start on the taken slot, but its hour runs into it.
        var (_, error) = await CreateService(db).CreateBookingAsync(BookingAt(MondayAt0830Utc));

        Assert.Equal(BookingError.SlotTaken, error);
    }

    [Fact]
    public async Task CreateBooking_RejectsTimeThatIsNotAPublishedSlotStart()
    {
        await using var db = CreateContext();

        // 09:15 — a quarter past, so off the 30-minute grid entirely.
        var (_, error) = await CreateService(db)
            .CreateBookingAsync(BookingAt(MondayAt0900Utc.AddMinutes(15)));

        Assert.Equal(BookingError.NotASlotStart, error);
    }

    [Fact]
    public async Task CreateBooking_RejectsTimeAfterClosing()
    {
        await using var db = CreateContext();

        // 16:00 would run to 17:00, half an hour past the last bookable start.
        var (_, error) = await CreateService(db)
            .CreateBookingAsync(BookingAt(MondayAt1530Utc.AddMinutes(30)));

        Assert.Equal(BookingError.NotASlotStart, error);
    }

    [Fact]
    public async Task CreateBooking_RejectsSunday()
    {
        await using var db = CreateContext();
        // Sunday 2026-07-12 at 08:30 NZST.
        var sundayAt0830Utc = new DateTime(2026, 7, 11, 20, 30, 0, DateTimeKind.Utc);

        var (_, error) = await CreateService(db).CreateBookingAsync(BookingAt(sundayAt0830Utc));

        Assert.Equal(BookingError.NotASlotStart, error);
    }

    [Fact]
    public async Task CreateBooking_RejectsSlotThatHasAlreadyStarted()
    {
        await using var db = CreateContext();
        // Now is 12:00 NZST that Monday, so the 09:00 slot is in the past.
        var now = new DateTimeOffset(2026, 7, 6, 0, 0, 0, TimeSpan.Zero);

        var (_, error) = await CreateService(db, now).CreateBookingAsync(BookingAt(MondayAt0900Utc));

        Assert.Equal(BookingError.OutsideBookingWindow, error);
    }

    [Fact]
    public async Task CreateBooking_RejectsSlotBeyondTheEightWeekHorizon()
    {
        await using var db = CreateContext();
        // Tuesday 2026-09-01 at 08:30 NZST — more than 56 days after the 1 July "today".
        var beyondHorizonUtc = new DateTime(2026, 8, 31, 20, 30, 0, DateTimeKind.Utc);

        var (_, error) = await CreateService(db).CreateBookingAsync(BookingAt(beyondHorizonUtc));

        Assert.Equal(BookingError.OutsideBookingWindow, error);
    }
}
