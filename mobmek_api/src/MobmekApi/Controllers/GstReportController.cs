using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/gst/report")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
public class GstReportController(IGstReportService gstReportService) : ControllerBase
{
    /// <summary>
    /// Included-vs-excluded GST review for a date range. "Included" covers every account;
    /// "Excluded" leaves out Cash-type accounts. Review only — does not affect what's filed.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(GstReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GstReportDto>> Get(
        [FromQuery] DateOnly start, [FromQuery] DateOnly end, CancellationToken cancellationToken)
    {
        var report = await gstReportService.GetReportAsync(start, end, cancellationToken);
        return Ok(report);
    }
}
