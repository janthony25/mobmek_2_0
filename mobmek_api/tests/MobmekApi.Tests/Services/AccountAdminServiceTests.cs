using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using MobmekApi.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;

namespace MobmekApi.Tests.Services;

public class AccountAdminServiceTests
{
    private static IConfiguration CreateConfig(bool configured = true) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(configured
                ? new Dictionary<string, string?> { ["Email:Resend:ApiKey"] = "re_test_key" }
                : [])
            .Build();

    // Same minimal Identity DI graph as AccountServiceTests, plus RoleManager (AccountAdminService
    // validates/assigns roles). Registers the exact `db` instance (not a second AppDbContext
    // pointed at the same database name) so UserManager/RoleManager share one tracked context
    // with `db` — matching production, where a single scoped AppDbContext is injected into both
    // AccountAdminService and its UserManager/RoleManager. Two separate context instances used
    // to work by accident for read-only/independent-write tests, but broke as soon as a test
    // needed to load an entity via `db` and then delete it via `userManager` in the same flow
    // (EF's "entity already tracked by a different instance" conflict) — a real trap, not a
    // theoretical one.
    private static (AppDbContext Db, UserManager<ApplicationUser> UserManager, RoleManager<IdentityRole<Guid>> RoleManager) CreateContext()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddLogging();
        services.AddDataProtection();
        services.AddIdentityCore<ApplicationUser>(options => options.Password.RequiredLength = 10)
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        var provider = services.BuildServiceProvider();
        return (db, provider.GetRequiredService<UserManager<ApplicationUser>>(), provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>());
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        foreach (var role in new[] { "Admin", "Employee" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }
    }

    private static async Task<Employee> SeedEmployeeAsync(AppDbContext db, string email = "new.hire@example.com")
    {
        var title = await new EmployeeTitleService(db).CreateAsync(new CreateEmployeeTitleRequest("Mechanic"));
        var type = await new EmploymentTypeService(db).CreateAsync(new CreateEmploymentTypeRequest("Full-time"));
        var (employee, _) = await new EmployeeService(db).CreateAsync(
            new CreateEmployeeRequest("New", "Hire", title.Id, type.Id, "0211234567", email, "1 Main St"));
        return (await db.Employees.FindAsync(employee!.Id))!;
    }

    private static async Task<ApplicationUser> SeedAdminUserAsync(
        AppDbContext db, UserManager<ApplicationUser> userManager, string email = "admin@example.com")
    {
        var employee = await SeedEmployeeAsync(db, email);
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, EmployeeId = employee.Id };
        var result = await userManager.CreateAsync(user, "OldPassw0rd!1");
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(user, "Admin");
        return user;
    }

    private static AccountAdminService BuildService(
        AppDbContext db, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole<Guid>> roleManager,
        FakeEmailSender sender, bool configured = true)
    {
        var config = CreateConfig(configured);
        return new(db, userManager, roleManager, sender, new EmailSettingsService(db, config), config);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesUnconfirmedAccountAndEmailsLink()
    {
        var (db, userManager, roleManager) = CreateContext();
        await using var _ = db;
        await SeedRolesAsync(roleManager);
        var employee = await SeedEmployeeAsync(db);
        var sender = new FakeEmailSender();
        var service = BuildService(db, userManager, roleManager, sender);

        var (account, error) = await service.CreateAsync(new CreateAccountRequest(employee.Id, "new.hire@example.com", "Employee"));

        Assert.Equal(AccountAdminError.None, error);
        Assert.NotNull(account);
        Assert.False(account!.IsActive);
        Assert.Equal(new[] { "Employee" }, account.Roles);
        var message = Assert.Single(sender.SentMessages);
        Assert.Equal("new.hire@example.com", message.To);
        var user = await userManager.FindByEmailAsync("new.hire@example.com");
        Assert.NotNull(user);
        Assert.False(user!.EmailConfirmed);
    }

    [Fact]
    public async Task CreateAsync_EmployeeAlreadyHasAccount_ReturnsError()
    {
        var (db, userManager, roleManager) = CreateContext();
        await using var _ = db;
        await SeedRolesAsync(roleManager);
        var employee = await SeedEmployeeAsync(db);
        var sender = new FakeEmailSender();
        var service = BuildService(db, userManager, roleManager, sender);
        await service.CreateAsync(new CreateAccountRequest(employee.Id, "new.hire@example.com", "Employee"));

        var (account, error) = await service.CreateAsync(new CreateAccountRequest(employee.Id, "other@example.com", "Employee"));

        Assert.Equal(AccountAdminError.EmployeeAlreadyHasAccount, error);
        Assert.Null(account);
    }

    [Fact]
    public async Task CreateAsync_UnknownRole_ReturnsInvalidRole()
    {
        var (db, userManager, roleManager) = CreateContext();
        await using var _ = db;
        await SeedRolesAsync(roleManager);
        var employee = await SeedEmployeeAsync(db);
        var service = BuildService(db, userManager, roleManager, new FakeEmailSender());

        var (account, error) = await service.CreateAsync(new CreateAccountRequest(employee.Id, "new.hire@example.com", "SuperUser"));

        Assert.Equal(AccountAdminError.InvalidRole, error);
        Assert.Null(account);
    }

    [Fact]
    public async Task CreateAsync_NotConfigured_CreatesNoAccountAndSendsNoEmail()
    {
        var (db, userManager, roleManager) = CreateContext();
        await using var _ = db;
        await SeedRolesAsync(roleManager);
        var employee = await SeedEmployeeAsync(db);
        var sender = new FakeEmailSender();
        var service = BuildService(db, userManager, roleManager, sender, configured: false);

        var (account, error) = await service.CreateAsync(new CreateAccountRequest(employee.Id, "new.hire@example.com", "Employee"));

        Assert.Equal(AccountAdminError.NotConfigured, error);
        Assert.Null(account);
        Assert.Empty(sender.SentMessages);
        Assert.Null(await userManager.FindByEmailAsync("new.hire@example.com"));
    }

    [Fact]
    public async Task CreateAsync_SendFailure_RollsBackTheAccountSoTheEmployeeCanBeRetried()
    {
        var (db, userManager, roleManager) = CreateContext();
        await using var _ = db;
        await SeedRolesAsync(roleManager);
        var employee = await SeedEmployeeAsync(db);
        var sender = new FakeEmailSender();
        sender.EnqueueSendResult(new EmailSendResult(false, null, "boom"));
        var service = BuildService(db, userManager, roleManager, sender);

        var (account, error) = await service.CreateAsync(new CreateAccountRequest(employee.Id, "new.hire@example.com", "Employee"));

        Assert.Equal(AccountAdminError.SendFailed, error);
        Assert.Null(account);
        Assert.Null(await userManager.FindByEmailAsync("new.hire@example.com"));

        // Retrying for the same employee must not be blocked by the rolled-back attempt.
        var sender2 = new FakeEmailSender();
        var service2 = BuildService(db, userManager, roleManager, sender2);
        var (retryAccount, retryError) = await service2.CreateAsync(new CreateAccountRequest(employee.Id, "new.hire@example.com", "Employee"));
        Assert.Equal(AccountAdminError.None, retryError);
        Assert.NotNull(retryAccount);
    }

    [Fact]
    public async Task ConfirmAccountAsync_CorrectToken_ActivatesAccountAndSetsPassword()
    {
        var (db, userManager, roleManager) = CreateContext();
        await using var _ = db;
        await SeedRolesAsync(roleManager);
        var employee = await SeedEmployeeAsync(db);
        var sender = new FakeEmailSender();
        var service = BuildService(db, userManager, roleManager, sender);
        await service.CreateAsync(new CreateAccountRequest(employee.Id, "new.hire@example.com", "Employee"));
        var token = ExtractToken(sender.SentMessages[0].Html);

        var error = await service.ConfirmAccountAsync(new ConfirmAccountRequest(token, "BrandNewPassw0rd!1"));

        Assert.Equal(AccountAdminError.None, error);
        var user = await userManager.FindByEmailAsync("new.hire@example.com");
        Assert.True(user!.EmailConfirmed);
        Assert.True(await userManager.CheckPasswordAsync(user, "BrandNewPassw0rd!1"));
    }

    [Fact]
    public async Task ConfirmAccountAsync_WrongToken_LeavesAccountUnconfirmed()
    {
        var (db, userManager, roleManager) = CreateContext();
        await using var _ = db;
        await SeedRolesAsync(roleManager);
        var employee = await SeedEmployeeAsync(db);
        var sender = new FakeEmailSender();
        var service = BuildService(db, userManager, roleManager, sender);
        await service.CreateAsync(new CreateAccountRequest(employee.Id, "new.hire@example.com", "Employee"));

        var error = await service.ConfirmAccountAsync(new ConfirmAccountRequest("not-the-real-token", "BrandNewPassw0rd!1"));

        Assert.Equal(AccountAdminError.InvalidToken, error);
        var user = await userManager.FindByEmailAsync("new.hire@example.com");
        Assert.False(user!.EmailConfirmed);
    }

    [Fact]
    public async Task ConfirmAccountAsync_ExpiredToken_ReturnsTokenExpired()
    {
        var (db, userManager, roleManager) = CreateContext();
        await using var _ = db;
        await SeedRolesAsync(roleManager);
        var employee = await SeedEmployeeAsync(db);
        var sender = new FakeEmailSender();
        var service = BuildService(db, userManager, roleManager, sender);
        await service.CreateAsync(new CreateAccountRequest(employee.Id, "new.hire@example.com", "Employee"));
        var token = ExtractToken(sender.SentMessages[0].Html);
        var row = await db.EmailConfirmationCodes.SingleAsync();
        row.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var error = await service.ConfirmAccountAsync(new ConfirmAccountRequest(token, "BrandNewPassw0rd!1"));

        Assert.Equal(AccountAdminError.TokenExpired, error);
    }

    [Fact]
    public async Task GetInvitePreviewAsync_ValidToken_ReturnsEmployeeNameAndEmail()
    {
        var (db, userManager, roleManager) = CreateContext();
        await using var _ = db;
        await SeedRolesAsync(roleManager);
        var employee = await SeedEmployeeAsync(db);
        var sender = new FakeEmailSender();
        var service = BuildService(db, userManager, roleManager, sender);
        await service.CreateAsync(new CreateAccountRequest(employee.Id, "new.hire@example.com", "Employee"));
        var token = ExtractToken(sender.SentMessages[0].Html);

        var (preview, error) = await service.GetInvitePreviewAsync(token);

        Assert.Equal(AccountAdminError.None, error);
        Assert.Equal("new.hire@example.com", preview!.Email);
        Assert.Equal("New", preview.FirstName);
        Assert.Equal("Hire", preview.LastName);
    }

    [Fact]
    public async Task GetInvitePreviewAsync_UnknownToken_ReturnsInvalidToken()
    {
        var (db, userManager, roleManager) = CreateContext();
        await using var _ = db;
        var service = BuildService(db, userManager, roleManager, new FakeEmailSender());

        var (preview, error) = await service.GetInvitePreviewAsync("nope");

        Assert.Equal(AccountAdminError.InvalidToken, error);
        Assert.Null(preview);
    }

    [Fact]
    public async Task UpdateRoleAsync_ChangesRole()
    {
        var (db, userManager, roleManager) = CreateContext();
        await using var _ = db;
        await SeedRolesAsync(roleManager);
        var employee = await SeedEmployeeAsync(db);
        var sender = new FakeEmailSender();
        var service = BuildService(db, userManager, roleManager, sender);
        await service.CreateAsync(new CreateAccountRequest(employee.Id, "new.hire@example.com", "Employee"));
        var user = (await userManager.FindByEmailAsync("new.hire@example.com"))!;

        var (account, error) = await service.UpdateRoleAsync(user.Id, new UpdateAccountRoleRequest("Admin"), actingUserId: Guid.NewGuid());

        Assert.Equal(AccountAdminError.None, error);
        Assert.Equal(new[] { "Admin" }, account!.Roles);
    }

    [Fact]
    public async Task UpdateRoleAsync_LastAdmin_CannotBeDemoted()
    {
        var (db, userManager, roleManager) = CreateContext();
        await using var _ = db;
        await SeedRolesAsync(roleManager);
        var admin = await SeedAdminUserAsync(db, userManager);
        var service = BuildService(db, userManager, roleManager, new FakeEmailSender());

        var (account, error) = await service.UpdateRoleAsync(admin.Id, new UpdateAccountRoleRequest("Employee"), actingUserId: Guid.NewGuid());

        Assert.Equal(AccountAdminError.LastAdmin, error);
        Assert.Null(account);
        Assert.Equal(new[] { "Admin" }, await userManager.GetRolesAsync(admin));
    }

    [Fact]
    public async Task UpdateRoleAsync_UnknownUser_ReturnsUserNotFound()
    {
        var (db, userManager, roleManager) = CreateContext();
        await using var _ = db;
        await SeedRolesAsync(roleManager);
        var service = BuildService(db, userManager, roleManager, new FakeEmailSender());

        var (account, error) = await service.UpdateRoleAsync(Guid.NewGuid(), new UpdateAccountRoleRequest("Admin"), actingUserId: Guid.NewGuid());

        Assert.Equal(AccountAdminError.UserNotFound, error);
        Assert.Null(account);
    }

    [Fact]
    public async Task UpdateRoleAsync_ActingUserTargetsOwnAccount_ReturnsCannotEditOwnRole()
    {
        var (db, userManager, roleManager) = CreateContext();
        await using var _ = db;
        await SeedRolesAsync(roleManager);
        var admin = await SeedAdminUserAsync(db, userManager);
        await SeedAdminUserAsync(db, userManager, "second.admin@example.com");
        var service = BuildService(db, userManager, roleManager, new FakeEmailSender());

        // Even with a second Admin in play (so it's not blocked as the "last admin"), an Admin
        // still can't be the one to change their own role.
        var (account, error) = await service.UpdateRoleAsync(admin.Id, new UpdateAccountRoleRequest("Employee"), actingUserId: admin.Id);

        Assert.Equal(AccountAdminError.CannotEditOwnRole, error);
        Assert.Null(account);
        Assert.Equal(new[] { "Admin" }, await userManager.GetRolesAsync(admin));
    }

    [Fact]
    public async Task DeactivateAsync_ValidTarget_BlocksSignInAndStampsTimestamp()
    {
        var (db, userManager, roleManager) = CreateContext();
        await using var _ = db;
        await SeedRolesAsync(roleManager);
        var employee = await SeedEmployeeAsync(db);
        var sender = new FakeEmailSender();
        var service = BuildService(db, userManager, roleManager, sender);
        await service.CreateAsync(new CreateAccountRequest(employee.Id, "new.hire@example.com", "Employee"));
        var token = ExtractToken(sender.SentMessages[0].Html);
        await service.ConfirmAccountAsync(new ConfirmAccountRequest(token, "OriginalPassw0rd!1"));
        var user = (await userManager.FindByEmailAsync("new.hire@example.com"))!;

        var (account, error) = await service.DeactivateAsync(user.Id, actingUserId: Guid.NewGuid());

        Assert.Equal(AccountAdminError.None, error);
        Assert.NotNull(account!.DeactivatedAtUtc);
        Assert.True(await userManager.IsLockedOutAsync(user));
    }

    [Fact]
    public async Task DeactivateAsync_ActingUserTargetsOwnAccount_ReturnsCannotDeactivateSelf()
    {
        var (db, userManager, roleManager) = CreateContext();
        await using var _ = db;
        await SeedRolesAsync(roleManager);
        var admin = await SeedAdminUserAsync(db, userManager);
        await SeedAdminUserAsync(db, userManager, "second.admin@example.com");
        var service = BuildService(db, userManager, roleManager, new FakeEmailSender());

        var (account, error) = await service.DeactivateAsync(admin.Id, actingUserId: admin.Id);

        Assert.Equal(AccountAdminError.CannotDeactivateSelf, error);
        Assert.Null(account);
        Assert.False(await userManager.IsLockedOutAsync(admin));
    }

    [Fact]
    public async Task DeactivateAsync_LastAdmin_CannotBeDeactivated()
    {
        var (db, userManager, roleManager) = CreateContext();
        await using var _ = db;
        await SeedRolesAsync(roleManager);
        var admin = await SeedAdminUserAsync(db, userManager);
        var service = BuildService(db, userManager, roleManager, new FakeEmailSender());

        var (account, error) = await service.DeactivateAsync(admin.Id, actingUserId: Guid.NewGuid());

        Assert.Equal(AccountAdminError.LastAdmin, error);
        Assert.Null(account);
        Assert.False(await userManager.IsLockedOutAsync(admin));
    }

    [Fact]
    public async Task DeactivateAsync_UnknownUser_ReturnsUserNotFound()
    {
        var (db, userManager, roleManager) = CreateContext();
        await using var _ = db;
        await SeedRolesAsync(roleManager);
        var service = BuildService(db, userManager, roleManager, new FakeEmailSender());

        var (account, error) = await service.DeactivateAsync(Guid.NewGuid(), actingUserId: Guid.NewGuid());

        Assert.Equal(AccountAdminError.UserNotFound, error);
        Assert.Null(account);
    }

    [Fact]
    public async Task ReactivateAsync_ClearsDeactivationAndLiftsLockout()
    {
        var (db, userManager, roleManager) = CreateContext();
        await using var _ = db;
        await SeedRolesAsync(roleManager);
        var employee = await SeedEmployeeAsync(db);
        var service = BuildService(db, userManager, roleManager, new FakeEmailSender());
        await service.CreateAsync(new CreateAccountRequest(employee.Id, "new.hire@example.com", "Employee"));
        var user = (await userManager.FindByEmailAsync("new.hire@example.com"))!;
        await service.DeactivateAsync(user.Id, actingUserId: Guid.NewGuid());

        var (account, error) = await service.ReactivateAsync(user.Id);

        Assert.Equal(AccountAdminError.None, error);
        Assert.Null(account!.DeactivatedAtUtc);
        Assert.False(await userManager.IsLockedOutAsync(user));
    }

    [Fact]
    public async Task PurgeExpiredDeactivatedAccountsAsync_DeletesOnlyAccountsPastTheGracePeriod()
    {
        var (db, userManager, roleManager) = CreateContext();
        await using var _ = db;
        await SeedRolesAsync(roleManager);
        var recentEmployee = await SeedEmployeeAsync(db, "recent@example.com");
        var expiredEmployee = await SeedEmployeeAsync(db, "expired@example.com");
        var service = BuildService(db, userManager, roleManager, new FakeEmailSender());
        await service.CreateAsync(new CreateAccountRequest(recentEmployee.Id, "recent@example.com", "Employee"));
        await service.CreateAsync(new CreateAccountRequest(expiredEmployee.Id, "expired@example.com", "Employee"));
        var recentUser = (await userManager.FindByEmailAsync("recent@example.com"))!;
        var expiredUser = (await userManager.FindByEmailAsync("expired@example.com"))!;
        await service.DeactivateAsync(recentUser.Id, actingUserId: Guid.NewGuid());
        await service.DeactivateAsync(expiredUser.Id, actingUserId: Guid.NewGuid());
        // userManager owns its own AppDbContext instance (a separate DI graph pointed at the
        // same in-memory database) — updates must go through it, not the `db` in this test.
        recentUser.DeactivatedAtUtc = DateTime.UtcNow.AddDays(-10);
        await userManager.UpdateAsync(recentUser);
        expiredUser.DeactivatedAtUtc = DateTime.UtcNow.AddDays(-31);
        await userManager.UpdateAsync(expiredUser);

        var purged = await service.PurgeExpiredDeactivatedAccountsAsync();

        Assert.Equal(1, purged);
        Assert.NotNull(await userManager.FindByIdAsync(recentUser.Id.ToString()));
        Assert.Null(await userManager.FindByIdAsync(expiredUser.Id.ToString()));
    }

    [Fact]
    public async Task PurgeExpiredDeactivatedAccountsAsync_AlsoRemovesOrphanedConfirmationCodes()
    {
        var (db, userManager, roleManager) = CreateContext();
        await using var _ = db;
        await SeedRolesAsync(roleManager);
        var employee = await SeedEmployeeAsync(db);
        var service = BuildService(db, userManager, roleManager, new FakeEmailSender());
        await service.CreateAsync(new CreateAccountRequest(employee.Id, "new.hire@example.com", "Employee"));
        var user = (await userManager.FindByEmailAsync("new.hire@example.com"))!;
        await service.DeactivateAsync(user.Id, actingUserId: Guid.NewGuid());
        user.DeactivatedAtUtc = DateTime.UtcNow.AddDays(-31);
        await userManager.UpdateAsync(user);

        var purged = await service.PurgeExpiredDeactivatedAccountsAsync();

        Assert.Equal(1, purged);
        Assert.Empty(await db.EmailConfirmationCodes.Where(c => c.UserId == user.Id).ToListAsync());
    }

    private static string ExtractToken(string html)
    {
        var marker = "token=";
        var start = html.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = html.IndexOf('"', start);
        return html[start..end];
    }
}
