using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class EmploymentTypesController(IEmploymentTypeService typeService) : ControllerBase
{
    /// <summary>Returns all employment types.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmploymentTypeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EmploymentTypeDto>>> GetAll(CancellationToken cancellationToken)
    {
        var types = await typeService.GetAllAsync(cancellationToken);
        return Ok(types);
    }

    /// <summary>Returns a single employment type by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EmploymentTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmploymentTypeDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var type = await typeService.GetByIdAsync(id, cancellationToken);
        return type is null ? NotFound() : Ok(type);
    }

    /// <summary>Creates a new employment type.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmploymentTypeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmploymentTypeDto>> Create(CreateEmploymentTypeRequest request, CancellationToken cancellationToken)
    {
        var created = await typeService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates an existing employment type.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EmploymentTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmploymentTypeDto>> Update(Guid id, UpdateEmploymentTypeRequest request, CancellationToken cancellationToken)
    {
        var updated = await typeService.UpdateAsync(id, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Deletes an employment type.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await typeService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
