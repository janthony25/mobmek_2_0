using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

/// <summary>Starts an unauthenticated password reset for a user who's locked out and can't
/// sign in to use the normal <c>/account/password</c> flow. Always answered the same way
/// regardless of whether the email matches an account — see <c>AccountService.RequestForgotPasswordCodeAsync</c>.</summary>
public record ForgotPasswordRequest([Required, EmailAddress] string Email);

/// <summary>Verifies the code emailed by <c>/auth/forgot-password</c> and sets a new password,
/// with no session required.</summary>
public record ResetForgottenPasswordRequest(
    [Required, EmailAddress] string Email,
    [Required] string Code,
    [Required] string NewPassword);

/// <summary>The signed-in staff member, as returned by login and the session-check endpoint.
/// <c>Permissions</c> is what the frontend should actually gate UI on — not <c>Roles</c>, which
/// is just the role name(s); a role's permissions are admin-editable (see RolesController), so
/// "Admin" isn't a safe thing to branch on anymore.</summary>
public record CurrentUserDto(
    Guid Id,
    string Email,
    Guid EmployeeId,
    string FirstName,
    string LastName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
