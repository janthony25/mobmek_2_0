using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/planned-transactions")]
[Produces("application/json")]
public class PlannedTransactionsController(IPlannedTransactionService plannedService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PlannedTransactionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PlannedTransactionDto>>> GetAll(CancellationToken cancellationToken)
    {
        var planned = await plannedService.GetAllAsync(cancellationToken);
        return Ok(planned);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PlannedTransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlannedTransactionDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var planned = await plannedService.GetByIdAsync(id, cancellationToken);
        return planned is null ? NotFound() : Ok(planned);
    }

    /// <summary>Creates a planned one-off item.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PlannedTransactionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PlannedTransactionDto>> Create(CreatePlannedTransactionRequest request, CancellationToken cancellationToken)
    {
        var (planned, error) = await plannedService.CreateAsync(request, cancellationToken);
        if (error != PlannedTransactionWriteError.None)
        {
            return MapError(error);
        }

        return CreatedAtAction(nameof(GetById), new { id = planned!.Id }, planned);
    }

    /// <summary>Updates a planned item (only while its status is still "Planned").</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PlannedTransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlannedTransactionDto>> Update(Guid id, UpdatePlannedTransactionRequest request, CancellationToken cancellationToken)
    {
        var (planned, error) = await plannedService.UpdateAsync(id, request, cancellationToken);
        if (error != PlannedTransactionWriteError.None)
        {
            return MapError(error);
        }

        return Ok(planned);
    }

    /// <summary>Deletes a planned item (only while its status is still "Planned").</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var error = await plannedService.DeleteAsync(id, cancellationToken);
        return error == PlannedTransactionWriteError.None ? NoContent() : MapError(error);
    }

    private ActionResult MapError(PlannedTransactionWriteError error) => error switch
    {
        PlannedTransactionWriteError.NotFound => NotFound(),
        PlannedTransactionWriteError.AccountNotFound => BadRequest("The account does not exist."),
        PlannedTransactionWriteError.AccountArchived => BadRequest("The account is archived."),
        PlannedTransactionWriteError.CategoryNotFound => BadRequest("The category does not exist."),
        PlannedTransactionWriteError.InvalidDirection => BadRequest("Direction must be \"In\" or \"Out\"."),
        PlannedTransactionWriteError.InvalidScenarioTag => BadRequest("Scenario tag must be \"BestCase\" or \"WorstCase\" (omit for \"Always\")."),
        PlannedTransactionWriteError.InvalidStatus => BadRequest("Status must be \"Planned\", \"Posted\" or \"Cancelled\"."),
        PlannedTransactionWriteError.NonPositiveAmount => BadRequest("Amount must be greater than zero."),
        PlannedTransactionWriteError.DirectionMismatchesCategory => BadRequest("The category doesn't apply to that direction."),
        PlannedTransactionWriteError.NotEditableOnceTerminal => Conflict("Posted or cancelled items can't be edited or deleted."),
        _ => BadRequest(),
    };
}
