using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/outbound-emails")]
[Produces("application/json")]
public class OutboundEmailsController(IOutboundEmailService outboundEmailService) : ControllerBase
{
    /// <summary>The outbound send history, newest first; filter by customer/invoice/status/kind.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(OutboundEmailPageDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OutboundEmailPageDto>> GetPaged(
        [FromQuery] Guid? customerId, [FromQuery] Guid? invoiceId,
        [FromQuery] OutboundEmailStatus? status, [FromQuery] OutboundEmailKind? kind,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await outboundEmailService.GetPagedAsync(
            new OutboundEmailFilter(customerId, invoiceId, status, kind, page, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>Re-sends a failed or bounced email as a new send attempt; the original row is untouched.</summary>
    [HttpPost("{id:guid}/retry")]
    [ProducesResponseType(typeof(OutboundEmailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OutboundEmailDto>> Retry(Guid id, CancellationToken cancellationToken)
    {
        var (email, error) = await outboundEmailService.RetryAsync(id, cancellationToken);
        return error switch
        {
            EmailWriteError.None => Ok(email),
            EmailWriteError.NotFound => NotFound(),
            EmailWriteError.NotRetryable => Problem(
                detail: "Only a failed or bounced email can be retried.", statusCode: StatusCodes.Status400BadRequest),
            EmailWriteError.NotConfigured => Problem(
                detail: "Email sending isn't configured yet — ask an admin to set it up under Settings → Email.",
                statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>The exact HTML that was (or would be) sent, for display — not JSON-wrapped.</summary>
    [HttpGet("{id:guid}/preview")]
    [Produces("text/html")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Preview(Guid id, CancellationToken cancellationToken)
    {
        var html = await outboundEmailService.GetPreviewHtmlAsync(id, cancellationToken);
        return html is null ? NotFound() : Content(html, "text/html");
    }
}
