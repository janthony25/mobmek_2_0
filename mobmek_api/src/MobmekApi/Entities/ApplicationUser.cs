using Microsoft.AspNetCore.Identity;

namespace MobmekApi.Entities;

/// <summary>
/// Login credentials for a staff member. Identity owns the credential fields
/// (email/username, password hash, lockout); <see cref="EmployeeId"/> links back to the
/// HR record in <see cref="Entities.Employee"/>, which stays credential-free.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public required Guid EmployeeId { get; set; }

    public Employee? Employee { get; set; }

    /// <summary>Set by an Admin via <see cref="Services.AccountAdminService.DeactivateAsync"/>,
    /// which also sets Identity's own <c>LockoutEnd</c> to block sign-in immediately. Independent
    /// of <c>EmailConfirmed</c> — a deactivated account is a distinct state from a pending invite.
    /// <see cref="Services.AccountPurgeJob"/> hard-deletes accounts deactivated 30+ days ago.</summary>
    public DateTime? DeactivatedAtUtc { get; set; }
}
