using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MobmekApi.Tests.Services;

public class EmailSettingsServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static IConfiguration CreateConfig(string? apiKey = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(apiKey is null
                ? []
                : new Dictionary<string, string?> { ["Email:Resend:ApiKey"] = apiKey })
            .Build();

    [Fact]
    public async Task GetCurrentAsync_CreatesDefault_WhenNoneExists()
    {
        await using var db = CreateContext();
        var service = new EmailSettingsService(db, CreateConfig());

        var settings = await service.GetCurrentAsync();

        Assert.Equal("Mobmek Workshop", settings.FromName);
        Assert.True(settings.BccSelf);
        Assert.Equal(1, await db.EmailSettings.CountAsync());
    }

    [Fact]
    public async Task GetCurrentAsync_IsSingleton_AcrossMultipleCalls()
    {
        await using var db = CreateContext();
        var service = new EmailSettingsService(db, CreateConfig());

        var first = await service.GetCurrentAsync();
        var second = await service.GetCurrentAsync();

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await db.EmailSettings.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_ChangesFields_AndPersists()
    {
        await using var db = CreateContext();
        var service = new EmailSettingsService(db, CreateConfig());

        var updated = await service.UpdateAsync(new UpdateEmailSettingsRequest(
            "Jun's Garage", "accounts@jungarage.co.nz", "shop@jungarage.co.nz", false));

        Assert.Equal("Jun's Garage", updated.FromName);
        Assert.Equal("accounts@jungarage.co.nz", updated.FromAddress);
        Assert.Equal("shop@jungarage.co.nz", updated.ReplyToAddress);
        Assert.False(updated.BccSelf);
        Assert.Equal(1, await db.EmailSettings.CountAsync());
    }

    [Fact]
    public async Task ResendConfigured_ReflectsConfigurationPresence()
    {
        await using var db = CreateContext();

        var withoutKey = await new EmailSettingsService(db, CreateConfig()).GetCurrentAsync();
        Assert.False(withoutKey.ResendConfigured);

        var withKey = await new EmailSettingsService(db, CreateConfig("re_test_key")).GetCurrentAsync();
        Assert.True(withKey.ResendConfigured);
    }
}
