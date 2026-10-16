using System.Security.Claims;
using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

/// <summary>Connect/disconnect and status for the Xero OAuth2 connection (<c>docs/xero-integration-design.md</c>
/// §6). The actual OAuth redirect round-trip happens through the frontend (not a backend redirect
/// endpoint) so the browser's auth cookie — SameSite=Strict — is still attached when the
/// frontend's own callback page calls <c>complete</c>; see the design doc for why.</summary>
[ApiController]
[Route("api/integrations/xero")]
[Produces("application/json")]
[Authorize(Policy = Permissions.ManageIntegrations)]
public class XeroIntegrationController(IXeroAuthService xeroAuthService) : ControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType(typeof(XeroStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<XeroStatusDto>> GetStatus(CancellationToken cancellationToken)
        => Ok(await xeroAuthService.GetStatusAsync(cancellationToken));

    /// <summary>Returns the Xero consent URL — the frontend navigates the browser to it directly
    /// (<c>window.location.href = ...</c>), it isn't fetched/followed as an API call.</summary>
    [HttpPost("connect")]
    [ProducesResponseType(typeof(XeroConnectResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<XeroConnectResponseDto>> Connect(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(new XeroConnectResponseDto(await xeroAuthService.BuildAuthorizationUrlAsync(cancellationToken)));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Called by the frontend's callback page once Xero redirects back with <c>code</c>/<c>state</c>.</summary>
    [HttpPost("complete")]
    [ProducesResponseType(typeof(XeroStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<XeroStatusDto>> Complete(
        [FromBody] XeroCompleteConnectionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await xeroAuthService.CompleteConnectionAsync(request.Code, request.State, CurrentUserId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("disconnect")]
    [ProducesResponseType(typeof(XeroStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<XeroStatusDto>> Disconnect(CancellationToken cancellationToken)
        => Ok(await xeroAuthService.DisconnectAsync(cancellationToken));

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
