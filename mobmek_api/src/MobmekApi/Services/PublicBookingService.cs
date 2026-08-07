using System.Globalization;
using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

/// <summary>
/// Availability and booking for the public marketing site.
/// </summary>
/// <remarks>
/// Takes a <see cref="TimeProvider"/> rather than calling <c>DateTime.UtcNow</c> like the
/// rest of the services do: "now" is load-bearing here (past slots disappear, the horizon
/// slides forward daily), and the slot grid can't be tested against a clock it doesn't own.
/// </remarks>
public class PublicBookingService(
    AppDbContext db, TimeProvider timeProvider, IAppointmentChangeNotifier changeNotifier,
    IGoogleCalendarClient calendarClient)
    : IPublicBookingService
{
    /// <summary>IANA id; .NET maps it to the Windows id automatically if ever hosted there.</summary>
    public const string TimeZoneId = "Pacific/Auckland";

    /// <summary>Slot starts run from here…</summary>
    public static readonly TimeOnly FirstSlotStart = new(8, 30);

    /// <summary>…to here inclusive, so the last appointment runs 15:30–16:30.</summary>
    public static readonly TimeOnly LastSlotStart = new(15, 30);

    /// <summary>Gap between consecutive start times.</summary>
    public const int SlotIntervalMinutes = 30;

    /// <summary>
    /// How long a booking occupies. Longer than the interval on purpose — starts roll every
    /// 30 minutes but each takes an hour, so booking 09:00 also consumes 08:30 and 09:30.
    /// </summary>
    public const int AppointmentMinutes = 60;

    /// <summary>Eight weeks. Customers can also book later today, as long as the slot hasn't started.</summary>
    public const int BookingHorizonDays = 56;

    /// <summary>Cap on one request's span, so nobody can pull the whole horizon in one call.</summary>
    public const int MaxRangeDays = 31;

    private const int DefaultRangeDays = 7;

    private static readonly TimeZoneInfo WorkshopZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    public async Task<AvailabilityDto> GetAvailabilityAsync(
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, WorkshopZone));
        var lastBookableDate = today.AddDays(BookingHorizonDays);

        // Clamp into the bookable window, then cap the span. Asking for last month or for a
        // year out is not an error — it just yields the part of the window that overlaps.
        var fromDate = Clamp(from ?? today, today, lastBookableDate);
        var toDate = Clamp(to ?? fromDate.AddDays(DefaultRangeDays - 1), fromDate, lastBookableDate);
        if (toDate.DayNumber - fromDate.DayNumber >= MaxRangeDays)
        {
            toDate = fromDate.AddDays(MaxRangeDays - 1);
        }

        var busy = await GetBusyIntervalsAsync(
            ToUtc(fromDate, FirstSlotStart),
            ToUtc(toDate, LastSlotStart).AddMinutes(AppointmentMinutes),
            cancellationToken);

        var days = new List<BookingDayDto>();
        for (var date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            days.Add(IsTradingDay(date)
                ? new BookingDayDto(date, Open: true, BuildSlots(date, busy, nowUtc))
                : new BookingDayDto(date, Open: false, []));
        }

        return new AvailabilityDto(
            fromDate, toDate, TimeZoneId, SlotIntervalMinutes, AppointmentMinutes, days);
    }

    public async Task<(BookingResultDto? Booking, BookingError Error)> CreateBookingAsync(
        CreateBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        var startUtc = UtcKind.Normalize(request.StartUtc);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, WorkshopZone));

        var local = TimeZoneInfo.ConvertTimeFromUtc(startUtc, WorkshopZone);
        var localDate = DateOnly.FromDateTime(local);
        var localTime = TimeOnly.FromDateTime(local);

        if (!IsTradingDay(localDate) || !IsSlotStart(localTime) || ToUtc(localDate, localTime) != startUtc)
        {
            // The final round-trip check rejects instants that land mid-slot in local terms —
            // a client can't invent a start time that isn't one we actually published.
            return (null, BookingError.NotASlotStart);
        }

        if (startUtc <= nowUtc || localDate > today.AddDays(BookingHorizonDays))
        {
            return (null, BookingError.OutsideBookingWindow);
        }

        var endUtc = startUtc.AddMinutes(AppointmentMinutes);

        // Re-check against the calendar at write time rather than trusting the availability
        // the customer was shown, which may be minutes stale.
        var busy = await GetBusyIntervalsAsync(startUtc, endUtc, cancellationToken);
        if (busy.Count > 0)
        {
            return (null, BookingError.SlotTaken);
        }

        var appointment = new Appointment
        {
            Title = request.ServiceNeeded,
            StartUtc = startUtc,
            EndUtc = endUtc,
            Status = AppointmentStatus.Requested,
            Notes = request.Notes,
            ContactName = request.ContactName,
            ContactPhone = request.ContactPhone,
            ContactEmail = request.ContactEmail,
            VehicleDescription = request.VehicleDescription,
        };

        db.Appointments.Add(appointment);
        EnqueueCalendarUpsert(appointment.Id);
        await db.SaveChangesAsync(cancellationToken);
        changeNotifier.NotifyChanged();

        return (
            new BookingResultDto(appointment.Id, startUtc, endUtc, FormatLocal(localTime)),
            BookingError.None);
    }

    /// <summary>Unconditional enqueue for a brand-new booking — mirrors
    /// <c>AppointmentService.EnqueueCalendarUpsert</c> so a website booking reaches the
    /// Workshop calendar on the same 30-second cycle as one created in the app, instead of
    /// waiting for the next reconcile pass.</summary>
    private void EnqueueCalendarUpsert(Guid appointmentId)
    {
        if (!calendarClient.IsConfigured)
        {
            return;
        }

        db.CalendarSyncItems.Add(new CalendarSyncItem
        {
            Action = CalendarSyncAction.Upsert,
            AppointmentId = appointmentId,
        });
    }

    /// <summary>
    /// Appointments blocking any part of [<paramref name="fromUtc"/>, <paramref name="toUtc"/>).
    /// Expressed as "not cancelled, not a no-show" rather than a list of blocking statuses so
    /// that any status added later blocks by default — the safe direction to fail.
    /// </summary>
    private async Task<List<(DateTime StartUtc, DateTime EndUtc)>> GetBusyIntervalsAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var rows = await db.Appointments
            .AsNoTracking()
            .Where(a => a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow)
            .Where(a => a.EndUtc > fromUtc && a.StartUtc < toUtc)
            .Select(a => new { a.StartUtc, a.EndUtc })
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.StartUtc, r.EndUtc)).ToList();
    }

    private static List<BookingSlotDto> BuildSlots(
        DateOnly date, List<(DateTime StartUtc, DateTime EndUtc)> busy, DateTime nowUtc)
    {
        var slots = new List<BookingSlotDto>();

        for (var time = FirstSlotStart; time <= LastSlotStart; time = time.AddMinutes(SlotIntervalMinutes))
        {
            var startUtc = ToUtc(date, time);
            var endUtc = startUtc.AddMinutes(AppointmentMinutes);
            var available = startUtc > nowUtc
                && !busy.Any(b => b.StartUtc < endUtc && b.EndUtc > startUtc);

            slots.Add(new BookingSlotDto(startUtc, FormatLocal(time), available));
        }

        return slots;
    }

    /// <summary>Open Monday to Saturday; closed Sunday.</summary>
    private static bool IsTradingDay(DateOnly date) => date.DayOfWeek != DayOfWeek.Sunday;

    private static bool IsSlotStart(TimeOnly time)
    {
        if (time < FirstSlotStart || time > LastSlotStart)
        {
            return false;
        }

        var minutesIn = (time - FirstSlotStart).TotalMinutes;
        return minutesIn % SlotIntervalMinutes == 0;
    }

    /// <summary>
    /// Local wall time to UTC. Cannot throw for an invalid (skipped) time: New Zealand's DST
    /// transitions happen at 02:00/03:00, well outside the 08:30–15:30 trading window.
    /// </summary>
    private static DateTime ToUtc(DateOnly date, TimeOnly time) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(time, DateTimeKind.Unspecified), WorkshopZone);

    /// <summary>
    /// Rendered server-side so the page always shows workshop-local time. A customer browsing
    /// from another timezone must see "8:30 AM" — the time they turn up — not their own clock.
    /// </summary>
    private static string FormatLocal(TimeOnly time) =>
        time.ToString("h:mm tt", CultureInfo.InvariantCulture);

    private static DateOnly Clamp(DateOnly value, DateOnly min, DateOnly max) =>
        value < min ? min : value > max ? max : value;
}
