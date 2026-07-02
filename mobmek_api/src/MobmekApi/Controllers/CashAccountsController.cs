using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/cash-accounts")]
[Produces("application/json")]
public class CashAccountsController(ICashAccountService cashAccountService) : ControllerBase
{
    /// <summary>Lists cash accounts with derived balances; pass <c>?includeArchived=true</c> for archived ones too.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CashAccountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CashAccountDto>>> GetAll([FromQuery] bool includeArchived, CancellationToken cancellationToken)
    {
        var accounts = await cashAccountService.GetAllAsync(includeArchived, cancellationToken);
        return Ok(accounts);
    }

    /// <summary>Returns a single cash account with its derived balance.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CashAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CashAccountDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var account = await cashAccountService.GetByIdAsync(id, cancellationToken);
        return account is null ? NotFound() : Ok(account);
    }

    /// <summary>Creates a cash account.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CashAccountDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CashAccountDto>> Create(CreateCashAccountRequest request, CancellationToken cancellationToken)
    {
        var account = await cashAccountService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = account.Id }, account);
    }

    /// <summary>Updates a cash account (including archiving it).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CashAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CashAccountDto>> Update(Guid id, UpdateCashAccountRequest request, CancellationToken cancellationToken)
    {
        var account = await cashAccountService.UpdateAsync(id, request, cancellationToken);
        return account is null ? NotFound() : Ok(account);
    }

    /// <summary>Deletes an account with no transactions; one with ledger history returns 409 (archive it instead).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await cashAccountService.DeleteAsync(id, cancellationToken);
        return result switch
        {
            CashAccountDeleteResult.Deleted => NoContent(),
            CashAccountDeleteResult.NotFound => NotFound(),
            _ => Conflict("The account has transactions; archive it instead of deleting."),
        };
    }
}
