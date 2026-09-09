using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/jobs/{jobId:guid}/photos")]
[Produces("application/json")]
public class JobPhotosController(IJobPhotoService jobPhotoService) : ControllerBase
{
    private const long MaxPhotoBytes = 10 * 1024 * 1024;

    /// <summary>Returns the photos on a job (metadata only).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<JobPhotoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<JobPhotoDto>>> GetAll(Guid jobId, CancellationToken cancellationToken)
    {
        var photos = await jobPhotoService.GetAllAsync(jobId, cancellationToken);
        return Ok(photos);
    }

    /// <summary>
    /// Uploads a photo (max 10 MB) onto a job. The same endpoint serves both "choose a file"
    /// and "take a photo" — the device camera hands the browser an ordinary image file.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(JobPhotoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobPhotoDto>> Add(Guid jobId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0 || file.Length > MaxPhotoBytes)
        {
            return BadRequest("The photo must be between 1 byte and 10 MB.");
        }

        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Only image files can be attached as job photos.");
        }

        await using var content = file.OpenReadStream();
        var photo = await jobPhotoService.AddAsync(
            jobId, content, file.FileName, file.ContentType, file.Length, cancellationToken);

        return photo is null
            ? NotFound()
            : CreatedAtAction(nameof(GetContent), new { jobId, id = photo.Id }, photo);
    }

    /// <summary>Streams a photo's image bytes — hand this URL straight to an &lt;img src&gt;.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContent(Guid jobId, Guid id, CancellationToken cancellationToken)
    {
        var result = await jobPhotoService.GetContentAsync(jobId, id, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        var (photo, content) = result.Value;
        return File(content, photo.ContentType);
    }

    /// <summary>Removes a photo and its stored image.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid jobId, Guid id, CancellationToken cancellationToken)
    {
        var deleted = await jobPhotoService.DeleteAsync(jobId, id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
