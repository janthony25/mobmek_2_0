using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/cashflow/forecast")]
[Produces("application/json")]
public class CashFlowForecastController(IForecastService forecastService) : ControllerBase
{
    /// <summary>Projected daily balance series. <c>scenario</c> is "BestCase", "Expected" (default) or "WorstCase".</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ForecastResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ForecastResultDto>> Get(
        [FromQuery] int horizonDays = 30, [FromQuery] string? scenario = "Expected", CancellationToken cancellationToken = default)
    {
        var result = await forecastService.ProjectAsync(horizonDays, scenario, cancellationToken);
        return Ok(result);
    }
}
