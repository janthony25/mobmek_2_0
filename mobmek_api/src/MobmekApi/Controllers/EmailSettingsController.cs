using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/emailsettings")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
public class EmailSettingsController(IEmailSettingsService emailSettingsService, IOutboundEmailService outboundEmailService) : ControllerBase
{
    /// <summary>Returns the current email settings (secrets excluded; see <c>ResendConfigured</c>).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(EmailSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EmailSettingsDto>> Get(CancellationToken cancellationToken)
    {
        return Ok(await emailSettingsService.GetCurrentAsync(cancellationToken));
    }

    /// <summary>Updates the from-name/from-address/reply-to/BCC-self settings.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(EmailSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmailSettingsDto>> Update(UpdateEmailSettingsRequest request, CancellationToken cancellationToken)
    {
        return Ok(await emailSettingsService.UpdateAsync(request, cancellationToken));
    }

    /// <summary>Sends a test email to confirm the Resend configuration works end to end.</summary>
    [HttpPost("test")]
    [ProducesResponseType(typeof(OutboundEmailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OutboundEmailDto>> SendTest(SendTestEmailRequest request, CancellationToken cancellationToken)
    {
        var (email, error) = await outboundEmailService.SendTestEmailAsync(request.ToAddress, cancellationToken);
        return error == EmailWriteError.NotConfigured
            ? Problem(detail: "Email sending isn't configured yet — set the Resend API key.", statusCode: StatusCodes.Status400BadRequest)
            : Ok(email);
    }
}
