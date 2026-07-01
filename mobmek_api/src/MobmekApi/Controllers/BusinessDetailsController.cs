using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/business-details")]
[Produces("application/json")]
public class BusinessDetailsController(IBusinessDetailsService businessDetailsService) : ControllerBase
{
    /// <summary>Returns the workshop's current letterhead details.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(BusinessDetailsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BusinessDetailsDto>> Get(CancellationToken cancellationToken)
    {
        var details = await businessDetailsService.GetCurrentAsync(cancellationToken);
        return Ok(details);
    }

    /// <summary>Updates the workshop's letterhead details.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(BusinessDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BusinessDetailsDto>> Update(UpdateBusinessDetailsRequest request, CancellationToken cancellationToken)
    {
        var details = await businessDetailsService.UpdateAsync(request, cancellationToken);
        return Ok(details);
    }
}
