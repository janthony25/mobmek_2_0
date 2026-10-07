using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class CarServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static async Task<Guid> SeedCustomerAsync(AppDbContext db)
    {
        var customer = await new CustomerService(db).CreateAsync(
            new CreateCustomerRequest("Owner", "Person", "000", null, null, null));
        return customer.Id;
    }

    private static async Task<(Guid MakeId, Guid ModelId)> SeedMakeModelAsync(AppDbContext db, string make = "BMW", string model = "Z3")
    {
        var carMake = await new CarMakeService(db).CreateAsync(new CreateCarMakeRequest(make));
        var carModel = await new CarModelService(db).CreateAsync(new CreateCarModelRequest(carMake.Id, model));
        return (carMake.Id, carModel!.Id);
    }

    [Fact]
    public async Task CreateAsync_PersistsCar_AndResolvesMakeModelNames()
    {
        await using var db = CreateContext();
        var customerId = await SeedCustomerAsync(db);
        var (makeId, modelId) = await SeedMakeModelAsync(db);
        var service = new CarService(db);

        var (car, error) = await service.CreateAsync(
            new CreateCarRequest(customerId, makeId, modelId, 2020, "ABC123", "VIN123", "Red", "Petrol"));

        Assert.Equal(CarWriteError.None, error);
        Assert.NotNull(car);
        Assert.Equal(customerId, car!.CustomerId);
        Assert.Equal("BMW", car.CarMakeName);
        Assert.Equal("Z3", car.CarModelName);
        Assert.Equal(1, await db.Cars.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_ReturnsCustomerNotFound_WhenCustomerMissing()
    {
        await using var db = CreateContext();
        var (makeId, modelId) = await SeedMakeModelAsync(db);
        var service = new CarService(db);

        var (car, error) = await service.CreateAsync(
            new CreateCarRequest(Guid.NewGuid(), makeId, modelId, 2020, "ABC123", null, null, null));

        Assert.Null(car);
        Assert.Equal(CarWriteError.CustomerNotFound, error);
    }

    [Fact]
    public async Task CreateAsync_ReturnsModelNotInMake_WhenModelBelongsToAnotherMake()
    {
        await using var db = CreateContext();
        var customerId = await SeedCustomerAsync(db);
        var (makeId, _) = await SeedMakeModelAsync(db, "BMW", "Z3");
        var (_, otherModelId) = await SeedMakeModelAsync(db, "Toyota", "Prius");
        var service = new CarService(db);

        var (car, error) = await service.CreateAsync(
            new CreateCarRequest(customerId, makeId, otherModelId, 2020, "ABC123", null, null, null));

        Assert.Null(car);
        Assert.Equal(CarWriteError.ModelNotInMake, error);
    }

    [Fact]
    public async Task CreateAsync_ReturnsMakeNotFound_WhenMakeMissing()
    {
        await using var db = CreateContext();
        var customerId = await SeedCustomerAsync(db);
        var (_, modelId) = await SeedMakeModelAsync(db);
        var service = new CarService(db);

        var (car, error) = await service.CreateAsync(
            new CreateCarRequest(customerId, Guid.NewGuid(), modelId, 2020, "ABC123", null, null, null));

        Assert.Null(car);
        Assert.Equal(CarWriteError.MakeNotFound, error);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByCustomerId()
    {
        await using var db = CreateContext();
        var service = new CarService(db);
        var customerA = await SeedCustomerAsync(db);
        var customerB = await SeedCustomerAsync(db);
        var (makeId, modelId) = await SeedMakeModelAsync(db);
        await service.CreateAsync(new CreateCarRequest(customerA, makeId, modelId, 2021, "A1", null, null, null));
        await service.CreateAsync(new CreateCarRequest(customerA, makeId, modelId, 2018, "A2", null, null, null));
        await service.CreateAsync(new CreateCarRequest(customerB, makeId, modelId, 2022, "B1", null, null, null));

        var carsForA = await service.GetAllAsync(customerA);

        Assert.Equal(2, carsForA.Count);
        Assert.All(carsForA, c => Assert.Equal(customerA, c.CustomerId));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenMissing()
    {
        await using var db = CreateContext();
        var service = new CarService(db);

        Assert.Null(await service.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateAsync_ModifiesFields_AndStampsUpdatedAt()
    {
        await using var db = CreateContext();
        var customerId = await SeedCustomerAsync(db);
        var (makeId, modelId) = await SeedMakeModelAsync(db);
        var service = new CarService(db);
        var (created, _) = await service.CreateAsync(
            new CreateCarRequest(customerId, makeId, modelId, 2010, "OLD1", "v", "Blue", "Diesel"));

        var (updated, error) = await service.UpdateAsync(created!.Id,
            new UpdateCarRequest(makeId, modelId, 2011, "NEW1", null, "Green", "EV"));

        Assert.Equal(CarWriteError.None, error);
        Assert.NotNull(updated);
        Assert.Equal(2011, updated!.Year);
        Assert.Equal("Green", updated.Color);
        Assert.Null(updated.Vin);
        Assert.Equal("EV", updated.EngineType);
        Assert.NotNull(updated.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFound_WhenMissing()
    {
        await using var db = CreateContext();
        var (makeId, modelId) = await SeedMakeModelAsync(db);
        var service = new CarService(db);

        var (car, error) = await service.UpdateAsync(Guid.NewGuid(),
            new UpdateCarRequest(makeId, modelId, 2020, "R", null, null, null));

        Assert.Null(car);
        Assert.Equal(CarWriteError.NotFound, error);
    }

    [Fact]
    public async Task DeleteAsync_RemovesCar_AndReturnsTrue()
    {
        await using var db = CreateContext();
        var customerId = await SeedCustomerAsync(db);
        var (makeId, modelId) = await SeedMakeModelAsync(db);
        var service = new CarService(db);
        var (created, _) = await service.CreateAsync(
            new CreateCarRequest(customerId, makeId, modelId, 2020, "TMP1", null, null, null));

        Assert.True(await service.DeleteAsync(created!.Id));
        Assert.Equal(0, await db.Cars.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenMissing()
    {
        await using var db = CreateContext();
        var service = new CarService(db);

        Assert.False(await service.DeleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateAsync_ReturnsDuplicateRego_WhenRegoAlreadyInUse_CaseAndWhitespaceInsensitive()
    {
        await using var db = CreateContext();
        var customerId = await SeedCustomerAsync(db);
        var (makeId, modelId) = await SeedMakeModelAsync(db);
        var service = new CarService(db);
        await service.CreateAsync(new CreateCarRequest(customerId, makeId, modelId, 2020, "ABC123", null, null, null));

        var (car, error) = await service.CreateAsync(
            new CreateCarRequest(customerId, makeId, modelId, 2021, " abc123 ", null, null, null));

        Assert.Null(car);
        Assert.Equal(CarWriteError.DuplicateRego, error);
        Assert.Equal(1, await db.Cars.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_ReturnsDuplicateVin_WhenVinAlreadyInUse()
    {
        await using var db = CreateContext();
        var customerId = await SeedCustomerAsync(db);
        var (makeId, modelId) = await SeedMakeModelAsync(db);
        var service = new CarService(db);
        await service.CreateAsync(new CreateCarRequest(customerId, makeId, modelId, 2020, "REG1", "VIN123ABC", null, null));

        var (car, error) = await service.CreateAsync(
            new CreateCarRequest(customerId, makeId, modelId, 2021, "REG2", "vin123abc", null, null));

        Assert.Null(car);
        Assert.Equal(CarWriteError.DuplicateVin, error);
    }

    [Fact]
    public async Task CreateAsync_AllowsTwoCars_WithNoVin()
    {
        await using var db = CreateContext();
        var customerId = await SeedCustomerAsync(db);
        var (makeId, modelId) = await SeedMakeModelAsync(db);
        var service = new CarService(db);
        await service.CreateAsync(new CreateCarRequest(customerId, makeId, modelId, 2020, "REG1", null, null, null));

        var (car, error) = await service.CreateAsync(
            new CreateCarRequest(customerId, makeId, modelId, 2021, "REG2", null, null, null));

        Assert.NotNull(car);
        Assert.Equal(CarWriteError.None, error);
    }

    [Fact]
    public async Task UpdateAsync_AllowsKeepingItsOwnRego()
    {
        await using var db = CreateContext();
        var customerId = await SeedCustomerAsync(db);
        var (makeId, modelId) = await SeedMakeModelAsync(db);
        var service = new CarService(db);
        var (created, _) = await service.CreateAsync(
            new CreateCarRequest(customerId, makeId, modelId, 2020, "ABC123", null, null, null));

        var (updated, error) = await service.UpdateAsync(created!.Id,
            new UpdateCarRequest(makeId, modelId, 2021, "ABC123", null, "Blue", null));

        Assert.Equal(CarWriteError.None, error);
        Assert.NotNull(updated);
    }

    [Fact]
    public async Task UpdateAsync_AllowsEditingOtherFields_OnAPreExistingDuplicateRegoPair()
    {
        // Regression guard: legacy-imported data has real pairs of cars sharing a rego (7 pairs
        // confirmed in production as of 2026-10-07, predating this uniqueness check). Editing an
        // unrelated field on either one must not be blocked just because its sibling already
        // holds the same rego — only an actual *change* to a colliding rego should be rejected.
        await using var db = CreateContext();
        var customerId = await SeedCustomerAsync(db);
        var (makeId, modelId) = await SeedMakeModelAsync(db);
        var service = new CarService(db);
        // Bypass CarService's own check to simulate data that predates it (e.g. a legacy import).
        db.Cars.AddRange(
            new MobmekApi.Entities.Car { CustomerId = customerId, CarMakeId = makeId, CarModelId = modelId, Year = 2019, Rego = "DUP123" },
            new MobmekApi.Entities.Car { Id = Guid.NewGuid(), CustomerId = customerId, CarMakeId = makeId, CarModelId = modelId, Year = 2020, Rego = "DUP123" });
        await db.SaveChangesAsync();
        var second = await db.Cars.OrderBy(c => c.Year).LastAsync();

        var (updated, error) = await service.UpdateAsync(second.Id,
            new UpdateCarRequest(makeId, modelId, 2020, "DUP123", null, "Blue", null));

        Assert.Equal(CarWriteError.None, error);
        Assert.NotNull(updated);
        Assert.Equal("Blue", updated!.Color);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsDuplicateRego_WhenCollidingWithAnotherCar()
    {
        await using var db = CreateContext();
        var customerId = await SeedCustomerAsync(db);
        var (makeId, modelId) = await SeedMakeModelAsync(db);
        var service = new CarService(db);
        await service.CreateAsync(new CreateCarRequest(customerId, makeId, modelId, 2020, "TAKEN1", null, null, null));
        var (other, _) = await service.CreateAsync(new CreateCarRequest(customerId, makeId, modelId, 2020, "FREE1", null, null, null));

        var (updated, error) = await service.UpdateAsync(other!.Id,
            new UpdateCarRequest(makeId, modelId, 2020, "TAKEN1", null, null, null));

        Assert.Null(updated);
        Assert.Equal(CarWriteError.DuplicateRego, error);
    }
}
