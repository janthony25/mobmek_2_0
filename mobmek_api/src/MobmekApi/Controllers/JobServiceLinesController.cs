using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/jobs/{jobId:guid}/services")]
[Produces("application/json")]
public class JobServiceLinesController(IJobServiceLineService lineService) : ControllerBase
{
    /// <summary>Returns the catalog services attached to a job.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<JobServiceLineDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<JobServiceLineDto>>> GetAll(Guid jobId, CancellationToken cancellationToken)
    {
        var lines = await lineService.GetAllAsync(jobId, cancellationToken);
        return Ok(lines);
    }

    /// <summary>Returns a single service line on a job.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(JobServiceLineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobServiceLineDto>> GetById(Guid jobId, Guid id, CancellationToken cancellationToken)
    {
        var line = await lineService.GetByIdAsync(jobId, id, cancellationToken);
        return line is null ? NotFound() : Ok(line);
    }

    /// <summary>Attaches a catalog service to a job (price is snapshotted).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(JobServiceLineDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobServiceLineDto>> Create(Guid jobId, CreateJobServiceLineRequest request, CancellationToken cancellationToken)
    {
        var (line, error) = await lineService.CreateAsync(jobId, request, cancellationToken);
        if (error != JobServiceLineWriteError.None)
        {
            return MapError(error);
        }

        return CreatedAtAction(nameof(GetById), new { jobId, id = line!.Id }, line);
    }

    /// <summary>Updates a job service line's quantity.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(JobServiceLineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobServiceLineDto>> Update(Guid jobId, Guid id, UpdateJobServiceLineRequest request, CancellationToken cancellationToken)
    {
        var updated = await lineService.UpdateAsync(jobId, id, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Removes a service line from a job.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid jobId, Guid id, CancellationToken cancellationToken)
    {
        var deleted = await lineService.DeleteAsync(jobId, id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private ActionResult MapError(JobServiceLineWriteError error) => error switch
    {
        JobServiceLineWriteError.JobNotFound => Problem(detail: "Job does not exist.", statusCode: StatusCodes.Status404NotFound),
        JobServiceLineWriteError.ServiceNotFound => Problem(detail: "Catalog service does not exist.", statusCode: StatusCodes.Status400BadRequest),
        JobServiceLineWriteError.ServiceInactive => Problem(detail: "Catalog service is inactive.", statusCode: StatusCodes.Status400BadRequest),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
    };
}
