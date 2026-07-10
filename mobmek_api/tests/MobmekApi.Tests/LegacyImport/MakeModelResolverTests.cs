using Microsoft.EntityFrameworkCore;
using MobmekApi.Data;
using MobmekApi.Entities;
using MobmekApi.LegacyImport.Mappers;

namespace MobmekApi.Tests.LegacyImport;

public class MakeModelResolverTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task GetOrCreateMake_MatchesExistingCaseInsensitively()
    {
        await using var db = CreateContext();
        var seeded = new CarMake { Name = "Toyota" };
        db.CarMakes.Add(seeded);
        await db.SaveChangesAsync();

        var resolver = await MakeModelResolver.LoadAsync(db);
        var resolved = resolver.GetOrCreateMake("TOYOTA");

        Assert.Equal(seeded.Id, resolved.Id);
        Assert.Equal("Toyota", resolved.Name);
    }

    [Fact]
    public async Task GetOrCreateMake_CreatesOnce_AndReusesWithinTheRun()
    {
        await using var db = CreateContext();
        var resolver = await MakeModelResolver.LoadAsync(db);

        var first = resolver.GetOrCreateMake("Suzuki");
        var second = resolver.GetOrCreateMake("SUZUKI ");
        await db.SaveChangesAsync();

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await db.CarMakes.CountAsync());
    }

    [Fact]
    public async Task GetOrCreateMake_BlankName_FallsBackToUnknown()
    {
        await using var db = CreateContext();
        var resolver = await MakeModelResolver.LoadAsync(db);

        Assert.Equal("Unknown", resolver.GetOrCreateMake(null).Name);
        Assert.Equal("Unknown", resolver.GetOrCreateMake("  ").Name);
    }

    [Fact]
    public async Task GetOrCreateModel_MatchesSeededModelCaseInsensitively()
    {
        await using var db = CreateContext();
        var make = new CarMake { Name = "Toyota" };
        var seededModel = new CarModel { Name = "Hilux", CarMake = make };
        db.CarMakes.Add(make);
        db.CarModels.Add(seededModel);
        await db.SaveChangesAsync();

        var resolver = await MakeModelResolver.LoadAsync(db);
        var resolved = resolver.GetOrCreateModel(resolver.GetOrCreateMake("TOYOTA"), "HILUX");

        Assert.Equal(seededModel.Id, resolved.Id);
        Assert.Equal(1, await db.CarModels.CountAsync());
    }

    [Fact]
    public async Task GetOrCreateModel_SameNameUnderDifferentMakes_CreatesSeparateModels()
    {
        await using var db = CreateContext();
        var resolver = await MakeModelResolver.LoadAsync(db);

        var toyota = resolver.GetOrCreateMake("Toyota");
        var ford = resolver.GetOrCreateMake("Ford");
        var toyotaModel = resolver.GetOrCreateModel(toyota, "RANGER");
        var fordModel = resolver.GetOrCreateModel(ford, "RANGER");
        await db.SaveChangesAsync();

        Assert.NotEqual(toyotaModel.Id, fordModel.Id);
        Assert.Equal(2, await db.CarModels.CountAsync());
    }

    [Fact]
    public async Task GetOrCreateModel_NewModelUnderExistingMake_LinksToThatMake()
    {
        await using var db = CreateContext();
        var resolver = await MakeModelResolver.LoadAsync(db);

        var make = resolver.GetOrCreateMake("Nissan");
        var model = resolver.GetOrCreateModel(make, "LAFESTA");
        await db.SaveChangesAsync();

        Assert.Equal(make.Id, model.CarMakeId);
        Assert.Equal("LAFESTA", model.Name);
    }
}
