using Microsoft.EntityFrameworkCore;
using MobmekApi.Data;
using MobmekApi.Entities;
using MobmekApi.LegacyImport.Legacy;
using MobmekApi.LegacyImport.Mappers;

namespace MobmekApi.Tests.LegacyImport;

public class ServiceCatalogResolverTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static LegacyService Legacy(string name, decimal price = 100m) =>
        new(3, name, "desc", price, true, new DateTime(2024, 6, 15, 10, 0, 0), null);

    [Fact]
    public async Task GetOrCreate_ReusesSeededEntryCaseInsensitively()
    {
        await using var db = CreateContext();
        var seeded = new JobService { Name = "Oil Change", Price = 89m };
        db.JobServices.Add(seeded);
        await db.SaveChangesAsync();

        var resolver = await ServiceCatalogResolver.LoadAsync(db);
        var (service, reused) = resolver.GetOrCreate(Legacy("OIL CHANGE", price: 75m));

        Assert.True(reused);
        Assert.Equal(seeded.Id, service.Id);
        Assert.Equal(89m, service.Price); // catalog entry untouched
        Assert.Equal(1, await db.JobServices.CountAsync());
    }

    [Fact]
    public async Task GetOrCreate_CreatesMissingEntry_WithLegacyFields()
    {
        await using var db = CreateContext();
        var resolver = await ServiceCatalogResolver.LoadAsync(db);

        var (service, reused) = resolver.GetOrCreate(Legacy("Diagnostic Scan", price: 65m));
        await db.SaveChangesAsync();

        Assert.False(reused);
        Assert.Equal("Diagnostic Scan", service.Name);
        Assert.Equal(65m, service.Price);
        Assert.True(service.IsActive);
        Assert.Equal(1, await db.JobServices.CountAsync());
    }

    [Fact]
    public async Task GetOrCreate_SameNameTwice_CreatesOnlyOnce()
    {
        await using var db = CreateContext();
        var resolver = await ServiceCatalogResolver.LoadAsync(db);

        var (first, _) = resolver.GetOrCreate(Legacy("Full Service"));
        var (second, reused) = resolver.GetOrCreate(Legacy("FULL SERVICE"));
        await db.SaveChangesAsync();

        Assert.True(reused);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await db.JobServices.CountAsync());
    }
}
