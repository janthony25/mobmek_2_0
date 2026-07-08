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

        return new CurrentUserDto(
            user.Id,
            user.Email!,
            user.EmployeeId,
            user.Employee.FirstName,
            user.Employee.LastName,
            roles.ToArray());
    }
}
