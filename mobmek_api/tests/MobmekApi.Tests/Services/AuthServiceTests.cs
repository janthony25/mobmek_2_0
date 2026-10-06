using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MobmekApi.Tests.Services;

/// <summary>
/// Covers GetCurrentUserAsync's permissions lookup (added alongside role-based authorization) —
/// what /auth/me and login return, which the frontend now gates UI on instead of Roles.
/// </summary>
public class AuthServiceTests
{
    private static async Task<(AuthService Service, AppDbContext Db, UserManager<ApplicationUser> Users, RoleManager<IdentityRole<Guid>> Roles)> SetupAsync()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddLogging();
        services.AddDataProtection();
        services.AddIdentityCore<ApplicationUser>(options => options.Password.RequiredLength = 10)
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        return (new AuthService(db, userManager), db, userManager, roleManager);
    }

    private static async Task<ApplicationUser> CreateUserWithRoleAsync(
        AppDbContext db, UserManager<ApplicationUser> users, RoleManager<IdentityRole<Guid>> roles,
        string roleName, params string[] permissions)
    {
        var role = new IdentityRole<Guid>(roleName);
        await roles.CreateAsync(role);
        db.RolePermissions.AddRange(permissions.Select(p => new RolePermission { RoleId = role.Id, Permission = p }));
        await db.SaveChangesAsync();

        var title = await new EmployeeTitleService(db).CreateAsync(new CreateEmployeeTitleRequest("Mechanic"));
        var type = await new EmploymentTypeService(db).CreateAsync(new CreateEmploymentTypeRequest("Full-time"));
        var (employee, _) = await new EmployeeService(db).CreateAsync(
            new CreateEmployeeRequest("Jane", "Doe", title.Id, type.Id, "0211234567", "jane@example.com", "1 Main St"));

        var user = new ApplicationUser { UserName = "jane@example.com", Email = "jane@example.com", EmployeeId = employee!.Id };
        await users.CreateAsync(user, "Passw0rd!1");
        await users.AddToRoleAsync(user, roleName);
        return user;
    }

    [Fact]
    public async Task GetCurrentUserAsync_IncludesTheRolesGrantedPermissions()
    {
        var (service, db, users, roles) = await SetupAsync();
        var user = await CreateUserWithRoleAsync(db, users, roles, "Front Desk", Permissions.ManageEmployees, Permissions.AccessCashFlow);

        var result = await service.GetCurrentUserAsync(user.Id);

        Assert.Equal(["AccessCashFlow", "ManageEmployees"], result!.Permissions.OrderBy(p => p));
    }

    [Fact]
    public async Task GetCurrentUserAsync_ReturnsNoPermissionsWhenTheRoleHasNoGrants()
    {
        var (service, db, users, roles) = await SetupAsync();
        var user = await CreateUserWithRoleAsync(db, users, roles, "Employee");

        var result = await service.GetCurrentUserAsync(user.Id);

        Assert.Empty(result!.Permissions);
    }
}
