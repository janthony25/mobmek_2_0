using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/jobs/{jobId:guid}/labour")]
[Produces("application/json")]
public class LabourController(ILabourService labourService) : ControllerBase
{
    /// <summary>Returns the labour lines on a job.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<LabourDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LabourDto>>> GetAll(Guid jobId, CancellationToken cancellationToken)
    {
        var labour = await labourService.GetAllAsync(jobId, cancellationToken);
        return Ok(labour);
    }

    /// <summary>Returns a single labour line on a job.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LabourDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LabourDto>> GetById(Guid jobId, Guid id, CancellationToken cancellationToken)
    {
        var labour = await labourService.GetByIdAsync(jobId, id, cancellationToken);
        return labour is null ? NotFound() : Ok(labour);
    }

    /// <summary>Adds a labour line to a job.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(LabourDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LabourDto>> Create(Guid jobId, CreateLabourRequest request, CancellationToken cancellationToken)
    {
        var created = await labourService.CreateAsync(jobId, request, cancellationToken);
        if (created is null)
        {
            return Problem(detail: $"Job '{jobId}' does not exist.", statusCode: StatusCodes.Status404NotFound);
        }

        return CreatedAtAction(nameof(GetById), new { jobId, id = created.Id }, created);
    }

    /// <summary>Updates a labour line on a job.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(LabourDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LabourDto>> Update(Guid jobId, Guid id, UpdateLabourRequest request, CancellationToken cancellationToken)
    {
        var updated = await labourService.UpdateAsync(jobId, id, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Deletes a labour line from a job.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid jobId, Guid id, CancellationToken cancellationToken)
    {
        var deleted = await labourService.DeleteAsync(jobId, id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
