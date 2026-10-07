using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IAuthService authService,
    ILoginAttemptService loginAttemptService,
    IAccountService accountService) : ControllerBase
{
    /// <summary>Signs in with email + password and sets the auth cookie.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CurrentUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserDto>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        // UserName == Email for every account we create, so the username-based overload
        // below both looks up and checks the password without a separate existence check
        // that would make "unknown email" distinguishable from "wrong password".
        var result = await signInManager.PasswordSignInAsync(
            request.Email, request.Password, isPersistent: true, lockoutOnFailure: true);

        // Looked up after the fact purely for the audit trail (which employee, if any, this
        // email belongs to) — doesn't affect the response, so it can't leak account existence.
        var user = await userManager.FindByEmailAsync(request.Email);
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        if (!result.Succeeded)
        {
            var failureReason = result.IsLockedOut ? "LockedOut" : result.IsNotAllowed ? "EmailNotConfirmed" : "InvalidCredentials";
            await loginAttemptService.RecordAsync(
                request.Email, user?.EmployeeId, succeeded: false, failureReason, ipAddress, cancellationToken);

            // Distinct from the generic 401 below: a newly created account genuinely needs to
            // know it must confirm its email first, rather than assume the password is wrong.
            if (result.IsNotAllowed)
            {
                return Problem(
                    detail: "This account hasn't been activated yet — check your email for the activation link.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            // A deactivated account is also reported as IsLockedOut (same Identity mechanism as a
            // temporary failed-attempts lockout), but "try again later" would be actively
            // misleading here — it's permanent until an Admin reactivates it.
            if (result.IsLockedOut && user?.DeactivatedAtUtc is not null)
            {
                return Problem(
                    detail: "This account has been deactivated — contact an Admin.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            return Unauthorized();
        }

        await loginAttemptService.RecordAsync(
            request.Email, user!.EmployeeId, succeeded: true, failureReason: null, ipAddress, cancellationToken);

        var currentUser = await authService.GetCurrentUserAsync(user.Id, cancellationToken);
        return Ok(currentUser);
    }

    /// <summary>Starts a password reset for a user who's locked out and can't sign in to reach
    /// the authenticated <c>/account/password</c> flow. Always 204 — deliberately indistinguishable
    /// whether or not the email matches an account, see <see cref="IAccountService.RequestForgotPasswordCodeAsync"/>.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await accountService.RequestForgotPasswordCodeAsync(request.Email, cancellationToken);
        return NoContent();
    }

    /// <summary>Verifies the code emailed by <see cref="ForgotPassword"/> and sets a new password.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(ResetForgottenPasswordRequest request, CancellationToken cancellationToken)
    {
        var (error, errorMessage) = await accountService.ResetForgottenPasswordAsync(request, cancellationToken);
        return error switch
        {
            AccountError.None => NoContent(),
            AccountError.InvalidCode => Problem(
                detail: "That code is incorrect, has already been used, or doesn't match that email.",
                statusCode: StatusCodes.Status400BadRequest),
            AccountError.CodeExpired => Problem(detail: "That code has expired — request a new one.", statusCode: StatusCodes.Status400BadRequest),
            AccountError.WeakPassword => Problem(detail: errorMessage ?? "That password doesn't meet the requirements.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>Clears the auth cookie.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    /// <summary>Returns the signed-in user. The auth middleware 401s before this runs if there's no valid session.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserDto>> Me(CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        var currentUser = await authService.GetCurrentUserAsync(Guid.Parse(userId!), cancellationToken);
        return currentUser is null ? Unauthorized() : Ok(currentUser);
    }

    /// <summary>The login audit trail (successes and failures), newest first.</summary>
    [HttpGet("login-attempts")]
    [Authorize(Policy = Permissions.ManageAccounts)]
    [ProducesResponseType(typeof(LoginAttemptPageDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LoginAttemptPageDto>> GetLoginAttempts(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        return Ok(await loginAttemptService.GetPagedAsync(page, pageSize, cancellationToken));
    }
}
