using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class EmployeeTitlesController(IEmployeeTitleService titleService) : ControllerBase
{
    /// <summary>Returns all employee titles.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeTitleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EmployeeTitleDto>>> GetAll(CancellationToken cancellationToken)
    {
        var titles = await titleService.GetAllAsync(cancellationToken);
        return Ok(titles);
    }

    /// <summary>Returns a single title by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeTitleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeTitleDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var title = await titleService.GetByIdAsync(id, cancellationToken);
        return title is null ? NotFound() : Ok(title);
    }

    /// <summary>Creates a new title.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeTitleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeTitleDto>> Create(CreateEmployeeTitleRequest request, CancellationToken cancellationToken)
    {
        var created = await titleService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates an existing title.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeTitleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeTitleDto>> Update(Guid id, UpdateEmployeeTitleRequest request, CancellationToken cancellationToken)
    {
        var updated = await titleService.UpdateAsync(id, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Deletes a title.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await titleService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
