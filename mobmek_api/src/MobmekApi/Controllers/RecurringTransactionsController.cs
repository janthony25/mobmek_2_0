using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/recurring-transactions")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
public class RecurringTransactionsController(IRecurringTransactionService recurringService) : ControllerBase
{
    /// <summary>Lists recurring schedules with computed next-occurrence and monthly-equivalent amount.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RecurringTransactionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RecurringTransactionDto>>> GetAll(
        [FromQuery] bool includePaused, CancellationToken cancellationToken)
    {
        var recurring = await recurringService.GetAllAsync(includePaused, cancellationToken);
        return Ok(recurring);
    }

    /// <summary>Occurrences due on or before today that haven't been posted; pass <c>?autoPostOnly=true</c> for what the background job would post.</summary>
    [HttpGet("due")]
    [ProducesResponseType(typeof(IReadOnlyList<DueOccurrenceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DueOccurrenceDto>>> GetDue(
        [FromQuery] bool autoPostOnly, CancellationToken cancellationToken)
    {
        var due = await recurringService.GetDueOccurrencesAsync(DateOnly.FromDateTime(DateTime.UtcNow), autoPostOnly, cancellationToken);
        return Ok(due);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RecurringTransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecurringTransactionDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var recurring = await recurringService.GetByIdAsync(id, cancellationToken);
        return recurring is null ? NotFound() : Ok(recurring);
    }

    /// <summary>Creates a recurring schedule.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(RecurringTransactionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecurringTransactionDto>> Create(CreateRecurringTransactionRequest request, CancellationToken cancellationToken)
    {
        var (recurring, error) = await recurringService.CreateAsync(request, cancellationToken);
        if (error != RecurringTransactionWriteError.None)
        {
            return MapError(error);
        }

        return CreatedAtAction(nameof(GetById), new { id = recurring!.Id }, recurring);
    }

    /// <summary>Updates a recurring schedule.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(RecurringTransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecurringTransactionDto>> Update(Guid id, UpdateRecurringTransactionRequest request, CancellationToken cancellationToken)
    {
        var (recurring, error) = await recurringService.UpdateAsync(id, request, cancellationToken);
        if (error != RecurringTransactionWriteError.None)
        {
            return MapError(error);
        }

        return Ok(recurring);
    }

    /// <summary>Deletes a schedule (materialised history stays on the ledger, unlinked).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await recurringService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>Pauses or resumes a schedule.</summary>
    [HttpPost("{id:guid}/pause")]
    [ProducesResponseType(typeof(RecurringTransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecurringTransactionDto>> SetPaused(Guid id, [FromQuery] bool paused, CancellationToken cancellationToken)
    {
        var recurring = await recurringService.SetPausedAsync(id, paused, cancellationToken);
        return recurring is null ? NotFound() : Ok(recurring);
    }

    /// <summary>Materialises the occurrence on <paramref name="date"/> as a ledger transaction.</summary>
    [HttpPost("{id:guid}/post-occurrence")]
    [ProducesResponseType(typeof(CashTransactionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CashTransactionDto>> PostOccurrence(Guid id, [FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        var (transaction, error) = await recurringService.PostOccurrenceAsync(id, date, cancellationToken);
        return error == RecurringTransactionWriteError.None ? Ok(transaction) : MapError(error);
    }

    private ActionResult MapError(RecurringTransactionWriteError error) => error switch
    {
        RecurringTransactionWriteError.NotFound => NotFound(),
        RecurringTransactionWriteError.AccountNotFound => BadRequest("The account does not exist."),
        RecurringTransactionWriteError.AccountArchived => BadRequest("The account is archived."),
        RecurringTransactionWriteError.CategoryNotFound => BadRequest("The category does not exist."),
        RecurringTransactionWriteError.InvalidDirection => BadRequest("Direction must be \"In\" or \"Out\"."),
        RecurringTransactionWriteError.InvalidGstTreatment => BadRequest("GST treatment must be \"Taxable\", \"Exempt\" or \"ZeroRated\"."),
        RecurringTransactionWriteError.InvalidFrequency => BadRequest("Frequency must be Weekly, Fortnightly, Monthly, Quarterly or Annually."),
        RecurringTransactionWriteError.DirectionMismatchesCategory => BadRequest("The category doesn't apply to that direction."),
        RecurringTransactionWriteError.NonPositiveAmount => BadRequest("Amount must be greater than zero."),
        RecurringTransactionWriteError.InvalidInterval => BadRequest("Interval must be at least 1."),
        RecurringTransactionWriteError.OccurrenceNotDue => BadRequest("That date isn't an occurrence of this schedule."),
        RecurringTransactionWriteError.OccurrenceAlreadyPosted => Conflict("That occurrence has already been posted."),
        _ => BadRequest(),
    };
}
