using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

/// <summary>Admin-only account/role management, distinct from <see cref="AccountController"/>
/// (self-service). The confirm endpoints are the exception: a brand-new account uses them to
/// activate itself before it can sign in at all, so they can't require an Admin session.</summary>
[ApiController]
[Route("api/accounts")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
public class AccountsController(IAccountAdminService accountAdminService, UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>Every login account, with its linked employee, roles, and confirmed/active status.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AccountListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AccountListItemDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await accountAdminService.GetAllAsync(cancellationToken));
    }

    /// <summary>Creates a login account for an existing Employee and emails an activation link.
    /// The account can't sign in until that link is used via <see cref="ConfirmAccount"/>.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AccountListItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AccountListItemDto>> Create(CreateAccountRequest request, CancellationToken cancellationToken)
    {
        var (account, error) = await accountAdminService.CreateAsync(request, cancellationToken);
        if (error != AccountAdminError.None)
        {
            return MapCreateError(error);
        }

        return CreatedAtAction(nameof(GetAll), account);
    }

    /// <summary>Replaces an existing account's role(s) with a single new role.</summary>
    [HttpPut("{userId:guid}/role")]
    [ProducesResponseType(typeof(AccountListItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountListItemDto>> UpdateRole(Guid userId, UpdateAccountRoleRequest request, CancellationToken cancellationToken)
    {
        var (account, error) = await accountAdminService.UpdateRoleAsync(userId, request, CurrentUserId, cancellationToken);
        return error switch
        {
            AccountAdminError.None => Ok(account),
            AccountAdminError.UserNotFound => NotFound(),
            AccountAdminError.InvalidRole => Problem(detail: $"Role '{request.Role}' does not exist.", statusCode: StatusCodes.Status400BadRequest),
            AccountAdminError.LastAdmin => Problem(
                detail: "Can't change this account's role — it's the last remaining Admin.", statusCode: StatusCodes.Status400BadRequest),
            AccountAdminError.CannotEditOwnRole => Problem(
                detail: "You can't change your own role — ask another Admin to do it.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>Deactivates an account: blocks sign-in immediately and starts the 30-day
    /// countdown to <see cref="Services.AccountPurgeJob"/> hard-deleting it.</summary>
    [HttpPost("{userId:guid}/deactivate")]
    [ProducesResponseType(typeof(AccountListItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountListItemDto>> Deactivate(Guid userId, CancellationToken cancellationToken)
    {
        var (account, error) = await accountAdminService.DeactivateAsync(userId, CurrentUserId, cancellationToken);
        return error switch
        {
            AccountAdminError.None => Ok(account),
            AccountAdminError.UserNotFound => NotFound(),
            AccountAdminError.LastAdmin => Problem(
                detail: "Can't deactivate this account — it's the last remaining Admin.", statusCode: StatusCodes.Status400BadRequest),
            AccountAdminError.CannotDeactivateSelf => Problem(
                detail: "You can't deactivate your own account — ask another Admin to do it.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>Reactivates a deactivated account: lifts the sign-in block and cancels the
    /// pending 30-day deletion.</summary>
    [HttpPost("{userId:guid}/reactivate")]
    [ProducesResponseType(typeof(AccountListItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountListItemDto>> Reactivate(Guid userId, CancellationToken cancellationToken)
    {
        var (account, error) = await accountAdminService.ReactivateAsync(userId, cancellationToken);
        return error switch
        {
            AccountAdminError.None => Ok(account),
            AccountAdminError.UserNotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>Previews whose account an invite link belongs to, before asking for a password.
    /// Anonymous: the link is reached before the account can sign in at all.</summary>
    [HttpGet("confirm/{token}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AccountInvitePreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountInvitePreviewDto>> GetInvitePreview(string token, CancellationToken cancellationToken)
    {
        var (preview, error) = await accountAdminService.GetInvitePreviewAsync(token, cancellationToken);
        return error switch
        {
            AccountAdminError.None => Ok(preview),
            AccountAdminError.TokenExpired => Problem(detail: "This link has expired — ask an Admin to resend an invite.", statusCode: StatusCodes.Status400BadRequest),
            _ => NotFound(),
        };
    }

    /// <summary>Confirms a newly created account's email and sets its first password from the
    /// emailed invite link. Anonymous by design: the account has no way to sign in yet, so
    /// there's no session to require.</summary>
    [HttpPost("confirm")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmAccount(ConfirmAccountRequest request, CancellationToken cancellationToken)
    {
        var error = await accountAdminService.ConfirmAccountAsync(request, cancellationToken);
        return error switch
        {
            AccountAdminError.None => NoContent(),
            AccountAdminError.InvalidToken => Problem(detail: "That link is invalid or has already been used.", statusCode: StatusCodes.Status400BadRequest),
            AccountAdminError.TokenExpired => Problem(detail: "That link has expired — ask an Admin to resend an invite.", statusCode: StatusCodes.Status400BadRequest),
            AccountAdminError.WeakPassword => Problem(detail: "That password doesn't meet the requirements.", statusCode: StatusCodes.Status400BadRequest),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private ActionResult MapCreateError(AccountAdminError error) => error switch
    {
        AccountAdminError.EmployeeNotFound => Problem(detail: "That employee does not exist.", statusCode: StatusCodes.Status400BadRequest),
        AccountAdminError.EmployeeAlreadyHasAccount => Problem(detail: "That employee already has a login account.", statusCode: StatusCodes.Status400BadRequest),
        AccountAdminError.EmailInUse => Problem(detail: "That email is already in use by another account.", statusCode: StatusCodes.Status400BadRequest),
        AccountAdminError.InvalidRole => Problem(detail: "That role does not exist.", statusCode: StatusCodes.Status400BadRequest),
        AccountAdminError.NotConfigured => Problem(
            detail: "Email sending isn't configured yet — set it up under Settings → Email first.", statusCode: StatusCodes.Status400BadRequest),
        AccountAdminError.SendFailed => Problem(detail: "Couldn't send the confirmation email — try again in a moment.", statusCode: StatusCodes.Status400BadRequest),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
    };

    private Guid CurrentUserId => Guid.Parse(userManager.GetUserId(User)!);
}
