using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class JobServicesController(IJobServiceCatalogService catalog) : ControllerBase
{
    /// <summary>Returns catalog services. Pass <c>?activeOnly=true</c> to exclude inactive ones.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<JobServiceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<JobServiceDto>>> GetAll([FromQuery] bool? activeOnly, CancellationToken cancellationToken)
    {
        var services = await catalog.GetAllAsync(activeOnly, cancellationToken);
        return Ok(services);
    }

    /// <summary>Returns a single catalog service by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(JobServiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobServiceDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var service = await catalog.GetByIdAsync(id, cancellationToken);
        return service is null ? NotFound() : Ok(service);
    }

    /// <summary>Creates a new catalog service.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(JobServiceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<JobServiceDto>> Create(CreateJobServiceRequest request, CancellationToken cancellationToken)
    {
        var created = await catalog.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates an existing catalog service.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(JobServiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobServiceDto>> Update(Guid id, UpdateJobServiceRequest request, CancellationToken cancellationToken)
    {
        var updated = await catalog.UpdateAsync(id, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Deletes a catalog service.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await catalog.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
