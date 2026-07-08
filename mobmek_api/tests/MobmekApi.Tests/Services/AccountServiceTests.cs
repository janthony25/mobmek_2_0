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

public class AccountServiceTests
{
    private static IConfiguration CreateConfig(bool configured = true) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(configured
                ? new Dictionary<string, string?> { ["Email:Resend:ApiKey"] = "re_test_key" }
                : [])
            .Build();

    // UserManager needs the full Identity DI graph to construct — build a minimal service
    // provider pointed at the same named in-memory database as `db` so both see the same data.
    private static (AppDbContext Db, UserManager<ApplicationUser> UserManager) CreateContextWithUserManager()
    {
        var dbName = Guid.NewGuid().ToString();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddLogging();
        services.AddDataProtection(); // required by the default token providers (password reset tokens)
        services.AddIdentityCore<ApplicationUser>(options => options.Password.RequiredLength = 10)
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        var userManager = services.BuildServiceProvider().GetRequiredService<UserManager<ApplicationUser>>();
        return (db, userManager);
    }

    private static async Task<ApplicationUser> SeedUserAsync(AppDbContext db, UserManager<ApplicationUser> userManager, string email = "jane@example.com")
    {
        var title = await new EmployeeTitleService(db).CreateAsync(new CreateEmployeeTitleRequest("Mechanic"));
        var type = await new EmploymentTypeService(db).CreateAsync(new CreateEmploymentTypeRequest("Full-time"));
        var (employee, _) = await new EmployeeService(db).CreateAsync(
            new CreateEmployeeRequest("Jane", "Doe", title.Id, type.Id, "0211234567", email, "1 Main St"));

        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, EmployeeId = employee!.Id };
        var result = await userManager.CreateAsync(user, "OldPassw0rd!1");
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(e => e.Description)));
        return user;
    }

    private static AccountService BuildService(
        AppDbContext db, UserManager<ApplicationUser> userManager, FakeEmailSender sender, bool configured = true) =>
        new(db, userManager, sender, new EmailSettingsService(db, CreateConfig(configured)));

    [Fact]
    public async Task GetProfileAsync_ReturnsContactInfo_AndLoginEmail()
    {
        var (db, userManager) = CreateContextWithUserManager();
        await using var _ = db;
        var user = await SeedUserAsync(db, userManager);
        var service = BuildService(db, userManager, new FakeEmailSender());

        var profile = await service.GetProfileAsync(user.Id);

        Assert.NotNull(profile);
        Assert.Equal("Jane", profile!.FirstName);
        Assert.Equal("jane@example.com", profile.Email);
    }

    [Fact]
    public async Task UpdateProfileAsync_OnlyChangesNameContactAndAddress()
    {
        var (db, userManager) = CreateContextWithUserManager();
        await using var _ = db;
        var user = await SeedUserAsync(db, userManager);
        var service = BuildService(db, userManager, new FakeEmailSender());

        var updated = await service.UpdateProfileAsync(user.Id, new UpdateProfileRequest("Janet", "Doey", "0299999999", "2 Other St"));

        Assert.NotNull(updated);
        Assert.Equal("Janet", updated!.FirstName);
        Assert.Equal("Doey", updated.LastName);
        Assert.Equal("0299999999", updated.ContactNumber);
        Assert.Equal("2 Other St", updated.PhysicalAddress);
        Assert.Equal("jane@example.com", updated.Email); // login email untouched
    }

    [Fact]
    public async Task RequestPasswordChangeCodeAsync_NotConfigured_WritesNoRow()
    {
        var (db, userManager) = CreateContextWithUserManager();
        await using var _ = db;
        var user = await SeedUserAsync(db, userManager);
        var sender = new FakeEmailSender();
        var service = BuildService(db, userManager, sender, configured: false);

        var error = await service.RequestPasswordChangeCodeAsync(user.Id);

        Assert.Equal(AccountError.NotConfigured, error);
        Assert.Equal(0, await db.PasswordChangeCodes.CountAsync());
        Assert.Empty(sender.SentMessages);
    }

    [Fact]
    public async Task RequestPasswordChangeCodeAsync_SendsExactlyOneEmail_ToTheUsersAddress()
    {
        var (db, userManager) = CreateContextWithUserManager();
        await using var _ = db;
        var user = await SeedUserAsync(db, userManager);
        var sender = new FakeEmailSender();
        var service = BuildService(db, userManager, sender);

        var error = await service.RequestPasswordChangeCodeAsync(user.Id);

        Assert.Equal(AccountError.None, error);
        Assert.Equal(1, await db.PasswordChangeCodes.CountAsync());
        var message = Assert.Single(sender.SentMessages);
        Assert.Equal("jane@example.com", message.To);
    }

    [Fact]
    public async Task RequestPasswordChangeCodeAsync_ASecondRequest_InvalidatesTheFirstCode()
    {
        var (db, userManager) = CreateContextWithUserManager();
        await using var _ = db;
        var user = await SeedUserAsync(db, userManager);
        var sender = new FakeEmailSender();
        var service = BuildService(db, userManager, sender);

        await service.RequestPasswordChangeCodeAsync(user.Id);
        var firstCode = ExtractCode(sender.SentMessages[0].Html);
        await service.RequestPasswordChangeCodeAsync(user.Id);

        var (error, _) = await service.ConfirmPasswordChangeAsync(user.Id, new ConfirmPasswordChangeRequest(firstCode, "NewPassw0rd!1"));

        Assert.Equal(AccountError.InvalidCode, error);
    }

    [Fact]
    public async Task ConfirmPasswordChangeAsync_CorrectCode_ResetsThePassword()
    {
        var (db, userManager) = CreateContextWithUserManager();
        await using var _ = db;
        var user = await SeedUserAsync(db, userManager);
        var sender = new FakeEmailSender();
        var service = BuildService(db, userManager, sender);
        await service.RequestPasswordChangeCodeAsync(user.Id);
        var code = ExtractCode(sender.SentMessages[0].Html);

        Assert.True(await userManager.CheckPasswordAsync(user, "OldPassw0rd!1"));

        var (error, errorMessage) = await service.ConfirmPasswordChangeAsync(user.Id, new ConfirmPasswordChangeRequest(code, "NewPassw0rd!1"));

        Assert.Equal(AccountError.None, error);
        Assert.Null(errorMessage);
        Assert.False(await userManager.CheckPasswordAsync(user, "OldPassw0rd!1"));
        Assert.True(await userManager.CheckPasswordAsync(user, "NewPassw0rd!1"));
    }

    [Fact]
    public async Task ConfirmPasswordChangeAsync_WrongCode_ReturnsInvalidCode()
    {
        var (db, userManager) = CreateContextWithUserManager();
        await using var _ = db;
        var user = await SeedUserAsync(db, userManager);
        var sender = new FakeEmailSender();
        var service = BuildService(db, userManager, sender);
        await service.RequestPasswordChangeCodeAsync(user.Id);

        var (error, _) = await service.ConfirmPasswordChangeAsync(user.Id, new ConfirmPasswordChangeRequest("000000", "NewPassw0rd!1"));

        Assert.Equal(AccountError.InvalidCode, error);
        Assert.True(await userManager.CheckPasswordAsync(user, "OldPassw0rd!1")); // unchanged
    }

    [Fact]
    public async Task ConfirmPasswordChangeAsync_ExpiredCode_ReturnsCodeExpired()
    {
        var (db, userManager) = CreateContextWithUserManager();
        await using var _ = db;
        var user = await SeedUserAsync(db, userManager);
        var sender = new FakeEmailSender();
        var service = BuildService(db, userManager, sender);
        await service.RequestPasswordChangeCodeAsync(user.Id);
        var code = ExtractCode(sender.SentMessages[0].Html);

        var row = await db.PasswordChangeCodes.SingleAsync();
        row.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var (error, _) = await service.ConfirmPasswordChangeAsync(user.Id, new ConfirmPasswordChangeRequest(code, "NewPassw0rd!1"));

        Assert.Equal(AccountError.CodeExpired, error);
    }

    [Fact]
    public async Task ConfirmPasswordChangeAsync_SameCodeTwice_SecondAttemptFails()
    {
        var (db, userManager) = CreateContextWithUserManager();
        await using var _ = db;
        var user = await SeedUserAsync(db, userManager);
        var sender = new FakeEmailSender();
        var service = BuildService(db, userManager, sender);
        await service.RequestPasswordChangeCodeAsync(user.Id);
        var code = ExtractCode(sender.SentMessages[0].Html);
        await service.ConfirmPasswordChangeAsync(user.Id, new ConfirmPasswordChangeRequest(code, "NewPassw0rd!1"));

        var (error, _) = await service.ConfirmPasswordChangeAsync(user.Id, new ConfirmPasswordChangeRequest(code, "AnotherPassw0rd!1"));

        Assert.Equal(AccountError.InvalidCode, error);
    }

    [Fact]
    public async Task ConfirmPasswordChangeAsync_WeakPassword_ReturnsIdentityErrorMessage()
    {
        var (db, userManager) = CreateContextWithUserManager();
        await using var _ = db;
        var user = await SeedUserAsync(db, userManager);
        var sender = new FakeEmailSender();
        var service = BuildService(db, userManager, sender);
        await service.RequestPasswordChangeCodeAsync(user.Id);
        var code = ExtractCode(sender.SentMessages[0].Html);

        var (error, errorMessage) = await service.ConfirmPasswordChangeAsync(user.Id, new ConfirmPasswordChangeRequest(code, "weak"));

        Assert.Equal(AccountError.WeakPassword, error);
        Assert.NotNull(errorMessage);
    }

    private static string ExtractCode(string html)
    {
        var start = html.IndexOf("<strong>", StringComparison.Ordinal) + "<strong>".Length;
        var end = html.IndexOf("</strong>", StringComparison.Ordinal);
        return html[start..end];
    }
}
