using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/cash-flow-settings")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
public class CashFlowSettingsController(ICashFlowSettingsService settingsService) : ControllerBase
{
    /// <summary>Returns the invoice-payment account routing.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(CashFlowSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CashFlowSettingsDto>> Get(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetCurrentAsync(cancellationToken);
        return Ok(settings);
    }

    /// <summary>Updates the invoice-payment account routing.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(CashFlowSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CashFlowSettingsDto>> Update(UpdateCashFlowSettingsRequest request, CancellationToken cancellationToken)
    {
        var settings = await settingsService.UpdateAsync(request, cancellationToken);
        return settings is null
            ? BadRequest("One or more of the account ids does not reference an existing cash account.")
            : Ok(settings);
    }
}
