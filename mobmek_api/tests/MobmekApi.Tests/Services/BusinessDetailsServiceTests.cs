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

    private static IFileStorage CreateStorage() =>
        new LocalFileStorage(Path.Combine(Path.GetTempPath(), "mobmek-tests", Guid.NewGuid().ToString("N")));

    [Fact]
    public async Task GetCurrentAsync_CreatesDefault_WhenNoneExists()
    {
        await using var db = CreateContext();
        var service = new BusinessDetailsService(db, CreateStorage());

        var details = await service.GetCurrentAsync();

        Assert.Equal("Mobmek Workshop", details.Name);
        Assert.Null(details.Address);
        Assert.Equal(1, await db.BusinessDetails.CountAsync());
    }

    [Fact]
    public async Task GetCurrentAsync_IsSingleton_AcrossMultipleCalls()
    {
        await using var db = CreateContext();
        var service = new BusinessDetailsService(db, CreateStorage());

        var first = await service.GetCurrentAsync();
        var second = await service.GetCurrentAsync();

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await db.BusinessDetails.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_ChangesFields_AndPersists()
    {
        await using var db = CreateContext();
        var service = new BusinessDetailsService(db, CreateStorage());

        var request = new UpdateBusinessDetailsRequest(
            "Jun's Garage",
            "1 Main St",
            "shop@example.com",
            "0400 000 000",
            "09 123 4567",
            "12 345 678 901",
            "www.junsgarage.co.nz",
            "Account Name: Jun's Garage\nBank: ANZ\nAccount: 12-3456-7890123-00");
        var updated = await service.UpdateAsync(request);

        Assert.Equal("Jun's Garage", updated.Name);
        Assert.Equal("1 Main St", updated.Address);
        Assert.Equal("shop@example.com", updated.Email);
        Assert.Equal("0400 000 000", updated.BusinessPhone);
        Assert.Equal("09 123 4567", updated.Telephone);
        Assert.Equal("12 345 678 901", updated.GstNumber);
        Assert.Equal("www.junsgarage.co.nz", updated.Website);
        Assert.Equal("Account Name: Jun's Garage\nBank: ANZ\nAccount: 12-3456-7890123-00", updated.BankDetails);
        Assert.NotNull(updated.UpdatedAtUtc);

        var reloaded = await service.GetCurrentAsync();
        Assert.Equal("Jun's Garage", reloaded.Name);
        Assert.Equal(1, await db.BusinessDetails.CountAsync());
    }

    [Fact]
    public async Task UploadLogoAsync_SetsLogoUrl_AndReplacesPreviousFile()
    {
        await using var db = CreateContext();
        var storage = CreateStorage();
        var service = new BusinessDetailsService(db, storage);

        var first = await service.UploadLogoAsync(new MemoryStream([1, 2, 3]), "logo.png", "image/png");
        Assert.Equal("/business-details/logo", first.LogoUrl);
        var firstKey = (await db.BusinessDetails.SingleAsync()).LogoStorageKey!;

        var second = await service.UploadLogoAsync(new MemoryStream([4, 5, 6]), "logo2.png", "image/png");
        Assert.Equal("/business-details/logo", second.LogoUrl);

        Assert.Null(await storage.OpenReadAsync(firstKey));
    }

    [Fact]
    public async Task GetLogoAsync_ReturnsStoredContent_ThenNullAfterDelete()
    {
        await using var db = CreateContext();
        var service = new BusinessDetailsService(db, CreateStorage());

        Assert.Null(await service.GetLogoAsync());

        await service.UploadLogoAsync(new MemoryStream([9, 9, 9]), "logo.png", "image/png");
        var logo = await service.GetLogoAsync();
        Assert.NotNull(logo);
        Assert.Equal("logo.png", logo!.Value.FileName);
        Assert.Equal("image/png", logo.Value.ContentType);

        Assert.True(await service.DeleteLogoAsync());
        Assert.Null(await service.GetLogoAsync());
    }

    [Fact]
    public async Task DeleteLogoAsync_ReturnsFalse_WhenNoneSet()
    {
        await using var db = CreateContext();
        var service = new BusinessDetailsService(db, CreateStorage());

        Assert.False(await service.DeleteLogoAsync());
    }
}
