using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>
/// One bookable start time. Deliberately carries no information about *why* a slot is
/// unavailable — this is served anonymously to the public website, so it must never leak
/// customer names, phone numbers, or job details.
/// </summary>
public record BookingSlotDto(DateTime StartUtc, string StartLocal, bool Available);

/// <summary>
/// One calendar day. <paramref name="Open"/> is false on days the workshop never trades
/// (Sunday), which is different from a day that is open but fully booked — the latter is
/// open with every slot unavailable.
/// </summary>
public record BookingDayDto(DateOnly Date, bool Open, IReadOnlyList<BookingSlotDto> Slots);

/// <summary>Availability across a requested date range, in the workshop's local timezone.</summary>
public record AvailabilityDto(
    DateOnly FromDate,
    DateOnly ToDate,
    string TimeZone,
    int SlotIntervalMinutes,
    int AppointmentMinutes,
    IReadOnlyList<BookingDayDto> Days);

/// <summary>
/// A booking submitted from the public website. There is no customer account behind it, so
/// every contact field is required — this is the only way the workshop can reach them back.
/// </summary>
public record CreateBookingRequest(
    [Required] DateTime StartUtc,
    [Required, MaxLength(200)] string ContactName,
    [Required, MaxLength(30)] string ContactPhone,
    [Required, EmailAddress, MaxLength(256)] string ContactEmail,
    [Required, MaxLength(500)] string VehicleDescription,
    [Required, MaxLength(200)] string ServiceNeeded,
    [MaxLength(4000)] string? Notes);

/// <summary>
/// Confirmation handed back to the public website. Intentionally minimal: the id and the
/// agreed time, nothing about the workshop's other bookings.
/// </summary>
public record BookingResultDto(Guid ReferenceId, DateTime StartUtc, DateTime EndUtc, string StartLocal);

/// <summary>Why a public booking was rejected.</summary>
public enum BookingError
{
    None,

    /// <summary>The start time is not one of the published slot start times, or falls on a closed day.</summary>
    NotASlotStart,

    /// <summary>The start time is in the past or beyond the booking horizon.</summary>
    OutsideBookingWindow,

    /// <summary>Someone else took the slot first.</summary>
    SlotTaken,
}
