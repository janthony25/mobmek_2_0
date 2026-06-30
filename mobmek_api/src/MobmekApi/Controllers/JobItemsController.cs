using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class JobItemsController(IJobItemService jobItemService) : ControllerBase
{
    /// <summary>Returns job items, optionally filtered by job via <c>?jobId=</c>.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<JobItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<JobItemDto>>> GetAll([FromQuery] Guid? jobId, CancellationToken cancellationToken)
    {
        var items = await jobItemService.GetAllAsync(jobId, cancellationToken);
        return Ok(items);
    }

    /// <summary>Returns a single job item by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(JobItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobItemDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await jobItemService.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>Adds an item to a job.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(JobItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<JobItemDto>> Create(CreateJobItemRequest request, CancellationToken cancellationToken)
    {
        var created = await jobItemService.CreateAsync(request, cancellationToken);
        if (created is null)
        {
            return Problem(detail: $"Job '{request.JobId}' does not exist.", statusCode: StatusCodes.Status400BadRequest);
        }

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates a job item.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(JobItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobItemDto>> Update(Guid id, UpdateJobItemRequest request, CancellationToken cancellationToken)
    {
        var updated = await jobItemService.UpdateAsync(id, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Deletes a job item.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await jobItemService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
