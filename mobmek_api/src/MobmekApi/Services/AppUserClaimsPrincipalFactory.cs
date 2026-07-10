using System.Security.Claims;
using MobmekApi.Data;
using MobmekApi.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MobmekApi.Services;

/// <summary>
/// Identity's default claims (NameIdentifier, Name, role claims) don't include a display name —
/// ApplicationUser has no name fields of its own, only <see cref="ApplicationUser.EmployeeId"/>.
/// Adding the linked Employee's "First Last" as a claim here means it's looked up once at sign-in
/// (cached in the auth cookie for the session) rather than joined on every write, which is what
/// <see cref="AppDbContext.SaveChangesAsync(CancellationToken)"/> needs to stamp
/// <see cref="BaseEntity.UpdatedByName"/> cheaply.
/// </summary>
public class AppUserClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<IdentityOptions> options,
    AppDbContext db) : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<Guid>>(userManager, roleManager, options)
{
    public const string FullNameClaimType = "mobmek:fullname";

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        var employee = await db.Employees.AsNoTracking()
            .Where(e => e.Id == user.EmployeeId)
            .Select(e => new { e.FirstName, e.LastName })
            .FirstOrDefaultAsync();

        if (employee is not null)
        {
            identity.AddClaim(new Claim(FullNameClaimType, $"{employee.FirstName} {employee.LastName}"));
        }

        return identity;
    }
}
