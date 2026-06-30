using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class LabourController(ILabourService labourService) : ControllerBase
{
    /// <summary>Returns labour lines, optionally filtered by job via <c>?jobId=</c>.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<LabourDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LabourDto>>> GetAll([FromQuery] Guid? jobId, CancellationToken cancellationToken)
    {
        var labour = await labourService.GetAllAsync(jobId, cancellationToken);
        return Ok(labour);
    }

    /// <summary>Returns a single labour line by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LabourDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LabourDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var labour = await labourService.GetByIdAsync(id, cancellationToken);
        return labour is null ? NotFound() : Ok(labour);
    }

    /// <summary>Adds a labour line to a job.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(LabourDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LabourDto>> Create(CreateLabourRequest request, CancellationToken cancellationToken)
    {
        var created = await labourService.CreateAsync(request, cancellationToken);
        if (created is null)
        {
            return Problem(detail: $"Job '{request.JobId}' does not exist.", statusCode: StatusCodes.Status400BadRequest);
        }

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates a labour line.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(LabourDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LabourDto>> Update(Guid id, UpdateLabourRequest request, CancellationToken cancellationToken)
    {
        var updated = await labourService.UpdateAsync(id, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Deletes a labour line.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await labourService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
