using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class RemindersController(IReminderService reminderService) : ControllerBase
{
    /// <summary>
    /// Returns reminders (outstanding first, soonest due), optionally filtered by
    /// <c>?customerId=</c>, <c>?carId=</c> and <c>?includeDone=</c> (defaults to true).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ReminderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReminderDto>>> GetAll(
        [FromQuery] Guid? customerId,
        [FromQuery] Guid? carId,
        [FromQuery] bool includeDone = true,
        CancellationToken cancellationToken = default)
    {
        var reminders = await reminderService.GetAllAsync(customerId, carId, includeDone, cancellationToken);
        return Ok(reminders);
    }

    /// <summary>Returns a single reminder by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ReminderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReminderDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var reminder = await reminderService.GetByIdAsync(id, cancellationToken);
        return reminder is null ? NotFound() : Ok(reminder);
    }

    /// <summary>Creates a new reminder for a customer (optionally a car / from a template).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ReminderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReminderDto>> Create(CreateReminderRequest request, CancellationToken cancellationToken)
    {
        var (reminder, error) = await reminderService.CreateAsync(request, cancellationToken);
        if (error != ReminderWriteError.None)
        {
            return MapError(error);
        }

        return CreatedAtAction(nameof(GetById), new { id = reminder!.Id }, reminder);
    }

    /// <summary>Updates an existing reminder.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ReminderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReminderDto>> Update(Guid id, UpdateReminderRequest request, CancellationToken cancellationToken)
    {
        var (reminder, error) = await reminderService.UpdateAsync(id, request, cancellationToken);
        if (error != ReminderWriteError.None)
        {
            return MapError(error);
        }

        return Ok(reminder);
    }

    /// <summary>Deletes a reminder.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await reminderService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private ActionResult MapError(ReminderWriteError error) => error switch
    {
        ReminderWriteError.NotFound => NotFound(),
        ReminderWriteError.CustomerNotFound => Problem(detail: "Customer does not exist.", statusCode: StatusCodes.Status400BadRequest),
        ReminderWriteError.CarNotFound => Problem(detail: "Car does not exist.", statusCode: StatusCodes.Status400BadRequest),
        ReminderWriteError.CarNotOwnedByCustomer => Problem(detail: "The selected car does not belong to this customer.", statusCode: StatusCodes.Status400BadRequest),
        ReminderWriteError.TemplateNotFound => Problem(detail: "Reminder template does not exist.", statusCode: StatusCodes.Status400BadRequest),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
    };
}
