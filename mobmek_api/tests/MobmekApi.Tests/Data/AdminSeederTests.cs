using MobmekApi.Data;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MobmekApi.Tests.Data;

/// <summary>
/// Covers the permission-seeding half of <see cref="AdminSeeder"/> added alongside role-based
/// authorization: Admin must end up holding every entry in <see cref="Permissions.All"/> on
/// every startup (not just the first, since most real environments already have a bootstrap
/// Admin), Employee must hold none, and a re-run must not duplicate or error.
/// </summary>
public class AdminSeederTests
{
    private static async Task<(AppDbContext Db, RoleManager<IdentityRole<Guid>> Roles, UserManager<ApplicationUser> Users, IConfiguration Config)> SetupAsync()
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
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bootstrap:AdminEmail"] = "owner@example.com",
                ["Bootstrap:AdminPassword"] = "Passw0rd!1",
            })
            .Build();

        return (
            provider.GetRequiredService<AppDbContext>(),
            provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>(),
            provider.GetRequiredService<UserManager<ApplicationUser>>(),
            config);
    }

    [Fact]
    public async Task SeedAsync_GrantsAdminEveryPermissionInTheCatalog()
    {
        var (db, roles, users, config) = await SetupAsync();

        await AdminSeeder.SeedAsync(db, users, roles, config, NullLogger.Instance);

        var adminRole = await roles.FindByNameAsync("Admin");
        var granted = await db.RolePermissions.Where(rp => rp.RoleId == adminRole!.Id).Select(rp => rp.Permission).ToListAsync();
        Assert.Equal(Permissions.All.OrderBy(p => p), granted.OrderBy(p => p));
    }

    [Fact]
    public async Task SeedAsync_GrantsEmployeeNoPermissions()
    {
        var (db, roles, users, config) = await SetupAsync();

        await AdminSeeder.SeedAsync(db, users, roles, config, NullLogger.Instance);

        var employeeRole = await roles.FindByNameAsync("Employee");
        var granted = await db.RolePermissions.Where(rp => rp.RoleId == employeeRole!.Id).ToListAsync();
        Assert.Empty(granted);
    }

    [Fact]
    public async Task SeedAsync_RunningTwiceDoesNotDuplicateGrants()
    {
        var (db, roles, users, config) = await SetupAsync();

        await AdminSeeder.SeedAsync(db, users, roles, config, NullLogger.Instance);
        await AdminSeeder.SeedAsync(db, users, roles, config, NullLogger.Instance);

        var adminRole = await roles.FindByNameAsync("Admin");
        var granted = await db.RolePermissions.Where(rp => rp.RoleId == adminRole!.Id).Select(rp => rp.Permission).ToListAsync();
        Assert.Equal(Permissions.All.Count, granted.Count);
    }
}
