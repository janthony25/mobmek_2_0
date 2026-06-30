using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/jobs/{jobId:guid}/items")]
[Produces("application/json")]
public class JobItemsController(IJobItemService jobItemService) : ControllerBase
{
    /// <summary>Returns the items on a job.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<JobItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<JobItemDto>>> GetAll(Guid jobId, CancellationToken cancellationToken)
    {
        var items = await jobItemService.GetAllAsync(jobId, cancellationToken);
        return Ok(items);
    }

    /// <summary>Returns a single item on a job.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(JobItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobItemDto>> GetById(Guid jobId, Guid id, CancellationToken cancellationToken)
    {
        var item = await jobItemService.GetByIdAsync(jobId, id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>Adds an item to a job.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(JobItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobItemDto>> Create(Guid jobId, CreateJobItemRequest request, CancellationToken cancellationToken)
    {
        var created = await jobItemService.CreateAsync(jobId, request, cancellationToken);
        if (created is null)
        {
            return Problem(detail: $"Job '{jobId}' does not exist.", statusCode: StatusCodes.Status404NotFound);
        }

        return CreatedAtAction(nameof(GetById), new { jobId, id = created.Id }, created);
    }

    /// <summary>Updates an item on a job.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(JobItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobItemDto>> Update(Guid jobId, Guid id, UpdateJobItemRequest request, CancellationToken cancellationToken)
    {
        var updated = await jobItemService.UpdateAsync(jobId, id, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Deletes an item from a job.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid jobId, Guid id, CancellationToken cancellationToken)
    {
        var deleted = await jobItemService.DeleteAsync(jobId, id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
