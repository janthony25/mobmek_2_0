using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

/// <summary>Reminder templates are read by every signed-in user (any mechanic picking a preset
/// while adding a reminder on a job/car), so only the write endpoints are Admin-gated — not the
/// whole controller like the other Settings pages (Tax/BusinessDetails/EmailSettings), which
/// nobody but Admin needs to even view.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ReminderTemplatesController(IReminderTemplateService templateService) : ControllerBase
{
    /// <summary>Returns all reminder templates.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ReminderTemplateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReminderTemplateDto>>> GetAll(CancellationToken cancellationToken)
    {
        var templates = await templateService.GetAllAsync(cancellationToken);
        return Ok(templates);
    }

    /// <summary>Returns a single reminder template by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ReminderTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReminderTemplateDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var template = await templateService.GetByIdAsync(id, cancellationToken);
        return template is null ? NotFound() : Ok(template);
    }

    /// <summary>Creates a new reminder template.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType(typeof(ReminderTemplateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReminderTemplateDto>> Create(CreateReminderTemplateRequest request, CancellationToken cancellationToken)
    {
        var created = await templateService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates an existing reminder template.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ReminderTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReminderTemplateDto>> Update(Guid id, UpdateReminderTemplateRequest request, CancellationToken cancellationToken)
    {
        var updated = await templateService.UpdateAsync(id, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Deletes a reminder template.</summary>
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await templateService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
