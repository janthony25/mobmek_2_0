using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

/// <summary>Self-service account management — every signed-in user manages their own account
/// (identity comes from the auth cookie, same as <see cref="AuthController.Me"/>), no role gate.</summary>
[ApiController]
[Route("api/account")]
[Produces("application/json")]
public class AccountController(IAccountService accountService, UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>Returns the signed-in user's own profile.</summary>
    [HttpGet("profile")]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProfileDto>> GetProfile(CancellationToken cancellationToken)
    {
        var profile = await accountService.GetProfileAsync(CurrentUserId, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    /// <summary>Updates the signed-in user's own name/contact info. Title, employment type,
    /// and login email stay Admin-managed via Employees.</summary>
    [HttpPut("profile")]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProfileDto>> UpdateProfile(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await accountService.UpdateProfileAsync(CurrentUserId, request, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    /// <summary>Emails a 6-digit code to start a password change; no current password needed.</summary>
    [HttpPost("password/request-code")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestPasswordChangeCode(CancellationToken cancellationToken)
    {
        var error = await accountService.RequestPasswordChangeCodeAsync(CurrentUserId, cancellationToken);
        return error switch
        {
            AccountError.None => NoContent(),
            AccountError.NotConfigured => Problem(
                detail: "Email sending isn't configured yet — ask an admin to set it up under Settings → Email.",
                statusCode: StatusCodes.Status400BadRequest),
            AccountError.SendFailed => Problem(
                detail: "Couldn't send the code email — try again in a moment.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>Verifies the code and resets the password.</summary>
    [HttpPost("password/confirm")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmPasswordChange(ConfirmPasswordChangeRequest request, CancellationToken cancellationToken)
    {
        var (error, errorMessage) = await accountService.ConfirmPasswordChangeAsync(CurrentUserId, request, cancellationToken);
        return error switch
        {
            AccountError.None => NoContent(),
            AccountError.InvalidCode => Problem(detail: "That code is incorrect or has already been used.", statusCode: StatusCodes.Status400BadRequest),
            AccountError.CodeExpired => Problem(detail: "That code has expired — request a new one.", statusCode: StatusCodes.Status400BadRequest),
            AccountError.WeakPassword => Problem(detail: errorMessage ?? "That password doesn't meet the requirements.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private Guid CurrentUserId => Guid.Parse(userManager.GetUserId(User)!);
}
