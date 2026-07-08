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
}
