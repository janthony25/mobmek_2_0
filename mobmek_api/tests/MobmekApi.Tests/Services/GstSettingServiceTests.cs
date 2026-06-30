using MobmekApi.Data;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class GstSettingServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task GetCurrentAsync_CreatesDefault_WhenNoneExists()
    {
        await using var db = CreateContext();
        var service = new GstSettingService(db);

        var setting = await service.GetCurrentAsync();

        Assert.Equal(0.15m, setting.Rate);
        Assert.Equal(1, await db.GstSettings.CountAsync());
    }

    [Fact]
    public async Task GetCurrentAsync_IsSingleton_AcrossMultipleCalls()
    {
        await using var db = CreateContext();
        var service = new GstSettingService(db);

        var first = await service.GetCurrentAsync();
        var second = await service.GetCurrentAsync();

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await db.GstSettings.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_ChangesRate_AndPersists()
    {
        await using var db = CreateContext();
        var service = new GstSettingService(db);

        var updated = await service.UpdateAsync(0.10m);
        Assert.Equal(0.10m, updated.Rate);

        var reloaded = await service.GetCurrentAsync();
        Assert.Equal(0.10m, reloaded.Rate);
        Assert.Equal(1, await db.GstSettings.CountAsync());
    }
}
