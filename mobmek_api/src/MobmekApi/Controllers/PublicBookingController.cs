using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MobmekApi.Controllers;

/// <summary>
/// The only anonymous surface besides login, called by the public marketing site's booking
/// page. Route is <c>api/public/...</c> rather than the usual <c>api/[controller]</c> so that
/// "this is unauthenticated" is obvious from the URL alone — in a log, in a proxy rule, or in
/// a firewall config.
/// </summary>
[ApiController]
[Route("api/public/booking")]
[AllowAnonymous]
[EnableCors(CorsPolicies.PublicSite)]
[Produces("application/json")]
public class PublicBookingController(IPublicBookingService bookingService) : ControllerBase
{
    /// <summary>
    /// Returns the bookable slot grid between <c>?from=</c> and <c>?to=</c> (both optional
    /// <c>yyyy-MM-dd</c>, defaulting to the coming week). Slots carry availability only —
    /// never any detail about existing appointments.
    /// </summary>
    [HttpGet("availability")]
    [EnableRateLimiting(RateLimitPolicies.PublicBookingRead)]
    [ProducesResponseType(typeof(AvailabilityDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AvailabilityDto>> GetAvailability(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        return Ok(await bookingService.GetAvailabilityAsync(from, to, cancellationToken));
    }

    /// <summary>
    /// Requests a booking. Creates an appointment with status <c>Requested</c>, which holds
    /// the slot but is not confirmed until staff approve it in the workshop app.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.PublicBookingWrite)]
    [ProducesResponseType(typeof(BookingResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingResultDto>> Create(
        CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var (booking, error) = await bookingService.CreateBookingAsync(request, cancellationToken);

        return error switch
        {
            BookingError.None => Created($"/api/appointments/{booking!.ReferenceId}", booking),
            BookingError.NotASlotStart => Problem(
                detail: "That time is not an available appointment slot.",
                statusCode: StatusCodes.Status400BadRequest),
            BookingError.OutsideBookingWindow => Problem(
                detail: "That time is in the past or too far ahead to book.",
                statusCode: StatusCodes.Status400BadRequest),
            BookingError.SlotTaken => Problem(
                detail: "Sorry, that slot has just been taken. Please choose another time.",
                statusCode: StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }
}
