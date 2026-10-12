using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/jobs/{jobId:guid}/files")]
[Produces("application/json")]
public class JobFilesController(IJobFileService jobFileService) : ControllerBase
{
    // Same cap as job photos (see JobPhotosController) — stays under Kestrel's default 30 MB
    // request-body limit so no extra server config is needed.
    private const long MaxFileBytes = 25 * 1024 * 1024;

    /// <summary>Returns the files on a job (metadata only).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<JobFileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<JobFileDto>>> GetAll(Guid jobId, CancellationToken cancellationToken)
    {
        var files = await jobFileService.GetAllAsync(jobId, cancellationToken);
        return Ok(files);
    }

    /// <summary>Uploads a file (max 25 MB) onto a job. Unlike job photos, any file type is accepted.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(JobFileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobFileDto>> Add(Guid jobId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0 || file.Length > MaxFileBytes)
        {
            return BadRequest("The file must be between 1 byte and 25 MB.");
        }

        await using var content = file.OpenReadStream();
        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
        var jobFile = await jobFileService.AddAsync(
            jobId, content, file.FileName, contentType, file.Length, cancellationToken);

        return jobFile is null
            ? NotFound()
            : CreatedAtAction(nameof(GetContent), new { jobId, id = jobFile.Id }, jobFile);
    }

    /// <summary>Streams a file's bytes — hand this URL straight to an &lt;a href&gt; for download.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContent(Guid jobId, Guid id, CancellationToken cancellationToken)
    {
        var result = await jobFileService.GetContentAsync(jobId, id, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        var (jobFile, content) = result.Value;
        return File(content, jobFile.ContentType, jobFile.FileName);
    }

    /// <summary>Removes a file and its stored content.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid jobId, Guid id, CancellationToken cancellationToken)
    {
        var deleted = await jobFileService.DeleteAsync(jobId, id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
