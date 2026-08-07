using System.Threading.Channels;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AppointmentsController(
    IAppointmentService appointmentService, IAppointmentChangeNotifier changeNotifier)
    : ControllerBase
{
    /// <summary>
    /// Server-Sent Events stream: emits a <c>changed</c> event whenever an appointment is
    /// created, updated, or deleted — including a booking arriving from the public website —
    /// so the calendar page can refetch instantly instead of polling. Carries no data; it's a
    /// doorbell, not a payload. Requires the normal staff auth cookie like every other
    /// endpoint here (this route has no <c>[AllowAnonymous]</c>).
    /// </summary>
    [HttpGet("stream")]
    public async Task Stream(CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        // Buffering the response would defeat the whole point: the browser wouldn't see an
        // event until the buffer flushed or the connection closed. Only affects Kestrel
        // itself — the Docker/nginx deployment needs its own proxy_buffering off (nginx.conf).
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        using var subscription = changeNotifier.Subscribe(out ChannelReader<byte> reader);

        // A comment (":" prefix) rather than a real event: EventSource fires onopen as soon
        // as headers arrive, but writing something immediately guards against a proxy in
        // between deciding an empty response is still "pending" and holding it back.
        await Response.WriteAsync(": connected\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            // A periodic comment keeps the connection alive through any intermediary that
            // times out an idle proxied connection, and doubles as a cheap dead-client check —
            // if the write fails, the loop's outer try naturally ends the response.
            waitCts.CancelAfter(TimeSpan.FromSeconds(20));

            try
            {
                await reader.ReadAsync(waitCts.Token);
                await Response.WriteAsync("event: changed\ndata: {}\n\n", cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // The 20s heartbeat timer fired, not a real disconnect.
                await Response.WriteAsync(": heartbeat\n\n", cancellationToken);
            }

            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Returns appointments overlapping <c>?from=</c>/<c>?to=</c> (both optional),
    /// optionally filtered by <c>?status=</c>, <c>?mechanicId=</c> and <c>?jobId=</c>.
    /// <c>?search=</c> matches title, contact name/phone, vehicle description, customer
    /// name and car rego.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AppointmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppointmentDto>>> GetAll(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] AppointmentStatus? status,
        [FromQuery] Guid? mechanicId,
        [FromQuery] Guid? jobId,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var appointments = await appointmentService.GetAllAsync(from, to, status, mechanicId, jobId, search, cancellationToken);
        return Ok(appointments);
    }

    /// <summary>Returns a single appointment by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var appointment = await appointmentService.GetByIdAsync(id, cancellationToken);
        return appointment is null ? NotFound() : Ok(appointment);
    }

    /// <summary>Creates a new appointment (linked customer or free-text contact).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AppointmentDto>> Create(CreateAppointmentRequest request, CancellationToken cancellationToken)
    {
        var (appointment, error) = await appointmentService.CreateAsync(request, cancellationToken);
        if (error != AppointmentWriteError.None)
        {
            return MapError(error);
        }

        return CreatedAtAction(nameof(GetById), new { id = appointment!.Id }, appointment);
    }

    /// <summary>Updates an existing appointment.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentDto>> Update(Guid id, UpdateAppointmentRequest request, CancellationToken cancellationToken)
    {
        var (appointment, error) = await appointmentService.UpdateAsync(id, request, cancellationToken);
        if (error != AppointmentWriteError.None)
        {
            return MapError(error);
        }

        return Ok(appointment);
    }

    /// <summary>Deletes an appointment.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await appointmentService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private ActionResult MapError(AppointmentWriteError error) => error switch
    {
        AppointmentWriteError.NotFound => NotFound(),
        AppointmentWriteError.EndNotAfterStart => Problem(detail: "End time must be after the start time.", statusCode: StatusCodes.Status400BadRequest),
        AppointmentWriteError.MissingContactOrCustomer => Problem(detail: "Link a customer or provide a contact name and phone.", statusCode: StatusCodes.Status400BadRequest),
        AppointmentWriteError.CustomerNotFound => Problem(detail: "Customer does not exist.", statusCode: StatusCodes.Status400BadRequest),
        AppointmentWriteError.CarNotFound => Problem(detail: "Car does not exist.", statusCode: StatusCodes.Status400BadRequest),
        AppointmentWriteError.CarNotOwnedByCustomer => Problem(detail: "The selected car does not belong to this customer.", statusCode: StatusCodes.Status400BadRequest),
        AppointmentWriteError.CarWithoutCustomer => Problem(detail: "A car can only be linked together with its customer.", statusCode: StatusCodes.Status400BadRequest),
        AppointmentWriteError.JobNotFound => Problem(detail: "Job does not exist.", statusCode: StatusCodes.Status400BadRequest),
        AppointmentWriteError.JobCustomerMismatch => Problem(detail: "The selected job belongs to a different customer.", statusCode: StatusCodes.Status400BadRequest),
        AppointmentWriteError.MechanicNotFound => Problem(detail: "Employee does not exist.", statusCode: StatusCodes.Status400BadRequest),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
    };
}
