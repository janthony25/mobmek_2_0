using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MobmekApi.Tests.Services;

public class RoleServiceTests
{
    private static async Task<(RoleService Service, RoleManager<IdentityRole<Guid>> Roles, UserManager<ApplicationUser> Users, AppDbContext Db)> SetupAsync()
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
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        await roleManager.CreateAsync(new IdentityRole<Guid>("Admin"));

        return (new RoleService(db, roleManager, userManager), roleManager, userManager, db);
    }

    [Fact]
    public async Task CreateAsync_CreatesANewRoleWithNoPermissionsYet()
    {
        var (service, _, _, _) = await SetupAsync();

        var (role, error) = await service.CreateAsync(new CreateRoleRequest("Front Desk"));

        Assert.Equal(RoleError.None, error);
        Assert.Equal("Front Desk", role!.Name);
        Assert.False(role.IsProtected);
        Assert.Equal(0, role.AccountCount);
        Assert.Empty(role.Permissions);
    }

    [Fact]
    public async Task CreateAsync_RejectsADuplicateName()
    {
        var (service, _, _, _) = await SetupAsync();
        await service.CreateAsync(new CreateRoleRequest("Front Desk"));

        var (role, error) = await service.CreateAsync(new CreateRoleRequest("Front Desk"));

        Assert.Equal(RoleError.DuplicateName, error);
        Assert.Null(role);
    }

    [Fact]
    public async Task SetPermissionsAsync_GrantsExactlyTheRequestedPermissions()
    {
        var (service, _, _, _) = await SetupAsync();
        var (created, _) = await service.CreateAsync(new CreateRoleRequest("Front Desk"));

        var (role, error) = await service.SetPermissionsAsync(
            created!.Id, new SetRolePermissionsRequest([Permissions.ManageEmployees, Permissions.ViewJobMargins]));

        Assert.Equal(RoleError.None, error);
        Assert.Equal(["ManageEmployees", "ViewJobMargins"], role!.Permissions.OrderBy(p => p));
    }

    [Fact]
    public async Task SetPermissionsAsync_ReplacesRatherThanAdding()
    {
        var (service, _, _, _) = await SetupAsync();
        var (created, _) = await service.CreateAsync(new CreateRoleRequest("Front Desk"));
        await service.SetPermissionsAsync(created!.Id, new SetRolePermissionsRequest([Permissions.ManageEmployees]));

        var (role, _) = await service.SetPermissionsAsync(created.Id, new SetRolePermissionsRequest([Permissions.AccessCashFlow]));

        Assert.Equal([Permissions.AccessCashFlow], role!.Permissions);
    }

    [Fact]
    public async Task SetPermissionsAsync_RejectsAPermissionOutsideTheCatalog()
    {
        var (service, _, _, _) = await SetupAsync();
        var (created, _) = await service.CreateAsync(new CreateRoleRequest("Front Desk"));

        var (role, error) = await service.SetPermissionsAsync(created!.Id, new SetRolePermissionsRequest(["NotARealPermission"]));

        Assert.Equal(RoleError.UnknownPermission, error);
        Assert.Null(role);
    }

    [Fact]
    public async Task SetPermissionsAsync_RefusesToTouchTheProtectedAdminRole()
    {
        var (service, roles, _, _) = await SetupAsync();
        var admin = await roles.FindByNameAsync("Admin");

        var (role, error) = await service.SetPermissionsAsync(admin!.Id, new SetRolePermissionsRequest([]));

        Assert.Equal(RoleError.Protected, error);
        Assert.Null(role);
    }

    [Fact]
    public async Task DeleteAsync_RemovesAnUnusedRoleAndItsGrants()
    {
        var (service, roles, _, db) = await SetupAsync();
        var (created, _) = await service.CreateAsync(new CreateRoleRequest("Front Desk"));
        await service.SetPermissionsAsync(created!.Id, new SetRolePermissionsRequest([Permissions.ManageEmployees]));

        var error = await service.DeleteAsync(created.Id);

        Assert.Equal(RoleError.None, error);
        Assert.Null(await roles.FindByIdAsync(created.Id.ToString()));
        Assert.Empty(db.RolePermissions.Where(rp => rp.RoleId == created.Id));
    }

    [Fact]
    public async Task DeleteAsync_RefusesToDeleteTheProtectedAdminRole()
    {
        var (service, roles, _, _) = await SetupAsync();
        var admin = await roles.FindByNameAsync("Admin");

        var error = await service.DeleteAsync(admin!.Id);

        Assert.Equal(RoleError.Protected, error);
        Assert.NotNull(await roles.FindByNameAsync("Admin"));
    }

    [Fact]
    public async Task DeleteAsync_RefusesToDeleteARoleStillAssignedToAnAccount()
    {
        var (service, roles, users, db) = await SetupAsync();
        var (created, _) = await service.CreateAsync(new CreateRoleRequest("Front Desk"));

        var title = await new EmployeeTitleService(db).CreateAsync(new CreateEmployeeTitleRequest("Mechanic"));
        var type = await new EmploymentTypeService(db).CreateAsync(new CreateEmploymentTypeRequest("Full-time"));
        var (employee, _) = await new EmployeeService(db).CreateAsync(
            new CreateEmployeeRequest("Jane", "Doe", title.Id, type.Id, "0211234567", "jane@example.com", "1 Main St"));
        var user = new ApplicationUser { UserName = "jane@example.com", Email = "jane@example.com", EmployeeId = employee!.Id };
        await users.CreateAsync(user, "Passw0rd!1");
        await users.AddToRoleAsync(user, "Front Desk");

        var error = await service.DeleteAsync(created!.Id);

        Assert.Equal(RoleError.InUse, error);
        Assert.NotNull(await roles.FindByIdAsync(created.Id.ToString()));
    }

    [Fact]
    public void GetPermissionCatalog_ReturnsTheFullFixedList()
    {
        var service = new RoleService(null!, null!, null!);

        Assert.Equal(Permissions.All, service.GetPermissionCatalog());
    }
}
