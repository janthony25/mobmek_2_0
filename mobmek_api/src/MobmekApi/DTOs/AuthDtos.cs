using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

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
