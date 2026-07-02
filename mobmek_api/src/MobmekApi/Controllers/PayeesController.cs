using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/payees")]
[Produces("application/json")]
public class PayeesController(IPayeeService payeeService) : ControllerBase
{
    /// <summary>All payees ordered by name; pass includeArchived=true to see archived ones.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PayeeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PayeeDto>>> GetAll(
        [FromQuery] bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        return Ok(await payeeService.GetAllAsync(includeArchived, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PayeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayeeDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var payee = await payeeService.GetByIdAsync(id, cancellationToken);
        return payee is null ? NotFound() : Ok(payee);
    }

    /// <summary>Spend history for one payee (count, first/last seen, 12-month in/out).</summary>
    [HttpGet("{id:guid}/summary")]
    [ProducesResponseType(typeof(PayeeSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayeeSummaryDto>> GetSummary(Guid id, CancellationToken cancellationToken)
    {
        var summary = await payeeService.GetSummaryAsync(id, cancellationToken);
        return summary is null ? NotFound() : Ok(summary);
    }

    [HttpPost]
    [ProducesResponseType(typeof(PayeeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PayeeDto>> Create(CreatePayeeRequest request, CancellationToken cancellationToken)
    {
        var (payee, error) = await payeeService.CreateAsync(request, cancellationToken);
        if (error != PayeeWriteError.None)
        {
            return MapError(error);
        }

        return CreatedAtAction(nameof(GetById), new { id = payee!.Id }, payee);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PayeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayeeDto>> Update(Guid id, UpdatePayeeRequest request, CancellationToken cancellationToken)
    {
        var (payee, error) = await payeeService.UpdateAsync(id, request, cancellationToken);
        return error == PayeeWriteError.None ? Ok(payee) : MapError(error);
    }

    /// <summary>Deletes a payee that has no history; payees in use are archived instead.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var error = await payeeService.DeleteAsync(id, cancellationToken);
        return error == PayeeWriteError.None ? NoContent() : MapError(error);
    }

    private ActionResult MapError(PayeeWriteError error) => error switch
    {
        PayeeWriteError.NotFound => NotFound(),
        PayeeWriteError.DuplicateName => BadRequest("A payee with that name already exists."),
        PayeeWriteError.CategoryNotFound => BadRequest("The default category does not exist."),
        PayeeWriteError.InvalidGstTreatment => BadRequest("GST treatment must be \"Taxable\", \"Exempt\" or \"ZeroRated\"."),
        PayeeWriteError.InUse => Conflict("This payee has transaction or rule history; archive it instead."),
        _ => BadRequest(),
    };
}
