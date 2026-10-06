using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class AuthService(AppDbContext db, UserManager<ApplicationUser> userManager) : IAuthService
{
    public async Task<CurrentUserDto?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users
            .AsNoTracking()
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user?.Employee is null)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);

        // Queried fresh here (not read off the signed-in cookie's claims) so this always
        // matches Roles above, which is also always fresh — same tradeoff that already existed
        // for Roles before permissions existed: a role's permissions changing mid-session isn't
        // reflected until the affected user's next login, same as AppUserClaimsPrincipalFactory.
        var permissions = await db.Roles
            .Where(r => roles.Contains(r.Name!))
            .Join(db.RolePermissions, r => r.Id, rp => rp.RoleId, (r, rp) => rp.Permission)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new CurrentUserDto(
            user.Id,
            user.Email!,
            user.EmployeeId,
            user.Employee.FirstName,
            user.Employee.LastName,
            roles.ToArray(),
            permissions);
    }
}
