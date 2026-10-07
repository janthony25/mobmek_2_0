using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

/// <summary>Editable outbound-email wording — the 3 seeded templates, never created/deleted.</summary>
[ApiController]
[Route("api/email-templates")]
[Produces("application/json")]
[Authorize(Policy = Permissions.ManageBusinessSettings)]
public class EmailTemplatesController(IEmailTemplateService emailTemplateService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmailTemplateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EmailTemplateDto>>> GetAll(CancellationToken cancellationToken)
    {
        var templates = await emailTemplateService.GetAllAsync(cancellationToken);
        return Ok(templates);
    }

    [HttpPut("{key}")]
    [ProducesResponseType(typeof(EmailTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmailTemplateDto>> Update(string key, UpdateEmailTemplateRequest request, CancellationToken cancellationToken)
    {
        var updated = await emailTemplateService.UpdateAsync(key, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Renders the currently-stored wording against dummy sample tokens, so the editor
    /// can show what it will look like.</summary>
    [HttpPost("{key}/preview")]
    [ProducesResponseType(typeof(EmailTemplatePreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmailTemplatePreviewDto>> Preview(string key, CancellationToken cancellationToken)
    {
        var preview = await emailTemplateService.PreviewAsync(key, cancellationToken);
        return preview is null ? NotFound() : Ok(preview);
    }
}
