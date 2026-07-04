using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/business-details")]
[Produces("application/json")]
public class BusinessDetailsController(IBusinessDetailsService businessDetailsService) : ControllerBase
{
    private const long MaxLogoBytes = 5 * 1024 * 1024;

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

    /// <summary>Uploads (replacing any existing) logo image shown on the invoice letterhead.</summary>
    [HttpPost("logo")]
    [ProducesResponseType(typeof(BusinessDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BusinessDetailsDto>> UploadLogo(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0 || file.Length > MaxLogoBytes)
        {
            return BadRequest("The logo must be between 1 byte and 5 MB.");
        }

        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("The logo must be an image file.");
        }

        await using var content = file.OpenReadStream();
        var details = await businessDetailsService.UploadLogoAsync(content, file.FileName, file.ContentType, cancellationToken);
        return Ok(details);
    }

    /// <summary>Downloads the current logo image.</summary>
    [HttpGet("logo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLogo(CancellationToken cancellationToken)
    {
        var result = await businessDetailsService.GetLogoAsync(cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        var (fileName, contentType, content) = result.Value;
        return File(content, contentType, fileName);
    }

    /// <summary>Removes the current logo image, if any.</summary>
    [HttpDelete("logo")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteLogo(CancellationToken cancellationToken)
    {
        var deleted = await businessDetailsService.DeleteLogoAsync(cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
