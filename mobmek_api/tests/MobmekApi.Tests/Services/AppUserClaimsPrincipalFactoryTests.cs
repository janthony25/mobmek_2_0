using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MobmekApi.Tests.Services;

/// <summary>
/// Regression coverage for a real bug: a first draft of AppUserClaimsPrincipalFactory inherited
/// from the single-generic-parameter <see cref="UserClaimsPrincipalFactory{TUser}"/>, which does
/// not add role claims at all — silently breaking every <c>[Authorize(Roles = "Admin")]</c> check
/// for every login, while <c>UserManager.GetRolesAsync</c> (used by AuthController's own
/// <c>/auth/me</c>) kept working because it queries the DB directly rather than reading the
/// cookie's claims. Only caught by manually exercising a real Admin-gated endpoint end to end —
/// no unit test exercised the actual claims principal until this file.
/// </summary>
public class AppUserClaimsPrincipalFactoryTests
{
    private static async Task<(IUserClaimsPrincipalFactory<ApplicationUser> Factory, ApplicationUser User)> SetupAsync(string role)
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddLogging();
        services.AddDataProtection();
        services.AddIdentityCore<ApplicationUser>(options => options.Password.RequiredLength = 10)
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>()
            .AddDefaultTokenProviders();

        var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        await roleManager.CreateAsync(new IdentityRole<Guid>(role));

        var title = await new EmployeeTitleService(db).CreateAsync(new CreateEmployeeTitleRequest("Mechanic"));
        var type = await new EmploymentTypeService(db).CreateAsync(new CreateEmploymentTypeRequest("Full-time"));
        var (employee, _) = await new EmployeeService(db).CreateAsync(
            new CreateEmployeeRequest("Jane", "Doe", title.Id, type.Id, "0211234567", "jane@example.com", "1 Main St"));

        var user = new ApplicationUser { UserName = "jane@example.com", Email = "jane@example.com", EmailConfirmed = true, EmployeeId = employee!.Id };
        var createResult = await userManager.CreateAsync(user, "Passw0rd!1");
        Assert.True(createResult.Succeeded, string.Join(" ", createResult.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(user, role);

        return (provider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>(), user);
    }

    [Fact]
    public async Task CreateAsync_IncludesTheUsersRoleClaim()
    {
        var (factory, user) = await SetupAsync("Admin");

        var principal = await factory.CreateAsync(user);

        Assert.True(principal.IsInRole("Admin"));
    }

    [Fact]
    public async Task CreateAsync_IncludesTheFullNameClaim()
    {
        var (factory, user) = await SetupAsync("Employee");

        var principal = await factory.CreateAsync(user);

        Assert.Equal("Jane Doe", principal.FindFirst(AppUserClaimsPrincipalFactory.FullNameClaimType)?.Value);
    }
}
