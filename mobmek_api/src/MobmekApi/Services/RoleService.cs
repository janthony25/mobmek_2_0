using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class RoleService(
    AppDbContext db,
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<ApplicationUser> userManager) : IRoleService
{
    private const string ProtectedRoleName = "Admin";

    public async Task<IReadOnlyList<RoleDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var roles = await roleManager.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync(cancellationToken);

        var result = new List<RoleDto>(roles.Count);
        foreach (var role in roles)
        {
            result.Add(await ToDtoAsync(role, cancellationToken));
        }

        return result;
    }

    public IReadOnlyList<string> GetPermissionCatalog() => Permissions.All;

    public async Task<(RoleDto? Role, RoleError Error)> CreateAsync(
        CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        if (await roleManager.RoleExistsAsync(request.Name))
        {
            return (null, RoleError.DuplicateName);
        }

        var role = new IdentityRole<Guid>(request.Name);
        var result = await roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            return (null, RoleError.DuplicateName);
        }

        return (await ToDtoAsync(role, cancellationToken), RoleError.None);
    }

    public async Task<(RoleDto? Role, RoleError Error)> SetPermissionsAsync(
        Guid roleId, SetRolePermissionsRequest request, CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByIdAsync(roleId.ToString());
        if (role is null)
        {
            return (null, RoleError.NotFound);
        }

        if (role.Name == ProtectedRoleName)
        {
            return (null, RoleError.Protected);
        }

        if (request.Permissions.Except(Permissions.All).Any())
        {
            return (null, RoleError.UnknownPermission);
        }

        var existing = await db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync(cancellationToken);
        db.RolePermissions.RemoveRange(existing);
        db.RolePermissions.AddRange(
            request.Permissions.Distinct().Select(p => new RolePermission { RoleId = roleId, Permission = p }));
        await db.SaveChangesAsync(cancellationToken);

        return (await ToDtoAsync(role, cancellationToken), RoleError.None);
    }

    public async Task<RoleError> DeleteAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByIdAsync(roleId.ToString());
        if (role is null)
        {
            return RoleError.NotFound;
        }

        if (role.Name == ProtectedRoleName)
        {
            return RoleError.Protected;
        }

        if ((await userManager.GetUsersInRoleAsync(role.Name!)).Count > 0)
        {
            return RoleError.InUse;
        }

        // No DB-level FK from RolePermission to AspNetRoles (see AppDbContext.OnModelCreating),
        // so this has to be cleaned up here — otherwise these rows sit around forever, orphaned,
        // referencing a RoleId nothing points to anymore.
        var grants = await db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync(cancellationToken);
        db.RolePermissions.RemoveRange(grants);
        await db.SaveChangesAsync(cancellationToken);

        await roleManager.DeleteAsync(role);
        return RoleError.None;
    }

    private async Task<RoleDto> ToDtoAsync(IdentityRole<Guid> role, CancellationToken cancellationToken)
    {
        var permissions = await db.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .Select(rp => rp.Permission)
            .ToListAsync(cancellationToken);
        var accountCount = (await userManager.GetUsersInRoleAsync(role.Name!)).Count;

        return new RoleDto(role.Id, role.Name!, role.Name == ProtectedRoleName, accountCount, permissions);
    }
}
