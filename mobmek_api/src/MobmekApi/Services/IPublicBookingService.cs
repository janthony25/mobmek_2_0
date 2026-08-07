using MobmekApi.DTOs;

namespace MobmekApi.Services;

/// <summary>
/// Serves the public website's booking page. Everything here is reachable anonymously, so
/// reads expose only free/busy and writes only ever create a <c>Requested</c> appointment
/// that staff must approve.
/// </summary>
public interface IPublicBookingService
{
    /// <summary>
    /// Returns the slot grid for <paramref name="from"/>..<paramref name="to"/> (inclusive),
    /// clamped to the bookable window and to a maximum span. Defaults to the coming week.
    /// </summary>
    Task<AvailabilityDto> GetAvailabilityAsync(
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>Books a slot, creating an appointment awaiting staff approval.</summary>
    Task<(BookingResultDto? Booking, BookingError Error)> CreateBookingAsync(
        CreateBookingRequest request,
        CancellationToken cancellationToken = default);
}
