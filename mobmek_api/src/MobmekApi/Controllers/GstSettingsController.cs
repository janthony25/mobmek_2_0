using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/gst")]
[Produces("application/json")]
public class GstSettingsController(IGstSettingService gstSettingService) : ControllerBase
{
    /// <summary>Returns the current GST setting (rate as a fraction, e.g. 0.15 = 15%).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(GstSettingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GstSettingDto>> Get(CancellationToken cancellationToken)
    {
        var setting = await gstSettingService.GetCurrentAsync(cancellationToken);
        return Ok(setting);
    }

    /// <summary>Updates the GST rate. New invoices snapshot this rate; existing invoices are unaffected.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(GstSettingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GstSettingDto>> Update(UpdateGstSettingRequest request, CancellationToken cancellationToken)
    {
        var setting = await gstSettingService.UpdateAsync(request.Rate, cancellationToken);
        return Ok(setting);
    }
}
