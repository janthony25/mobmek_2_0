using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class BusinessDetailsServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task GetCurrentAsync_CreatesDefault_WhenNoneExists()
    {
        await using var db = CreateContext();
        var service = new BusinessDetailsService(db);

        var details = await service.GetCurrentAsync();

        Assert.Equal("Mobmek Workshop", details.Name);
        Assert.Null(details.Address);
        Assert.Equal(1, await db.BusinessDetails.CountAsync());
    }

    [Fact]
    public async Task GetCurrentAsync_IsSingleton_AcrossMultipleCalls()
    {
        await using var db = CreateContext();
        var service = new BusinessDetailsService(db);

        var first = await service.GetCurrentAsync();
        var second = await service.GetCurrentAsync();

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await db.BusinessDetails.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_ChangesFields_AndPersists()
    {
        await using var db = CreateContext();
        var service = new BusinessDetailsService(db);

        var request = new UpdateBusinessDetailsRequest(
            "Jun's Garage",
            "1 Main St",
            "0400 000 000",
            "shop@example.com",
            "12 345 678 901");
        var updated = await service.UpdateAsync(request);

        Assert.Equal("Jun's Garage", updated.Name);
        Assert.Equal("1 Main St", updated.Address);
        Assert.Equal("0400 000 000", updated.Phone);
        Assert.Equal("shop@example.com", updated.Email);
        Assert.Equal("12 345 678 901", updated.Abn);
        Assert.NotNull(updated.UpdatedAtUtc);

        var reloaded = await service.GetCurrentAsync();
        Assert.Equal("Jun's Garage", reloaded.Name);
        Assert.Equal(1, await db.BusinessDetails.CountAsync());
    }
}
