using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>One row in the Admin-facing accounts/roles list. <c>IsActive</c> reflects
/// <c>EmailConfirmed</c> (has the invite been used yet) — independent of
/// <c>DeactivatedAtUtc</c>, which an Admin sets explicitly and blocks sign-in regardless of
/// confirmation state.</summary>
public record AccountListItemDto(
    Guid UserId,
    Guid EmployeeId,
    string FirstName,
    string LastName,
    string Email,
    IReadOnlyList<string> Roles,
    bool IsActive,
    DateTime? DeactivatedAtUtc);

/// <summary>
/// <paramref name="Password"/> is optional — leave it out to get a random password the new hire
/// never sees (the normal case, where they set their own via the emailed link). Set it to give
/// the account a known password up front; the new hire can change it later either way.
/// <paramref name="ActivateImmediately"/> skips the email/link entirely and confirms the account
/// at creation, so it can sign in right away — use when email isn't configured/reachable or
/// speed matters more than the invite-link flow.
/// </summary>
public record CreateAccountRequest(
    [Required] Guid EmployeeId,
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required] string Role,
    [MinLength(1)] string? Password = null,
    bool ActivateImmediately = false);

public record UpdateAccountRoleRequest([Required] string Role);

/// <summary>Confirms a new account from the emailed invite link. The token alone identifies the
/// account — no email needed, since the link is the whole point (nothing left to type by hand).</summary>
public record ConfirmAccountRequest(
    [Required] string Token,
    [Required] string NewPassword);

/// <summary>Shown on the confirm-account page before the new hire sets a password, so the link
/// visibly confirms whose account this is rather than dropping them straight into a bare form.</summary>
public record AccountInvitePreviewDto(string Email, string FirstName, string LastName);

/// <summary>Outcome of an Admin account-management operation that depends on state outside the request body.</summary>
public enum AccountAdminError
{
    None,
    EmployeeNotFound,
    EmployeeAlreadyHasAccount,
    EmailInUse,
    InvalidRole,
    UserNotFound,
    LastAdmin,
    CannotEditOwnRole,
    CannotDeactivateSelf,
    NotConfigured,
    SendFailed,
    InvalidToken,
    TokenExpired,
    WeakPassword,
    AlreadyConfirmed,
}
