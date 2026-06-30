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
            new CreateCarRequest(customerId, makeId, modelId, 2020, "ABC123", "VIN123", "Red", "Petrol", 50000));

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
            new CreateCarRequest(Guid.NewGuid(), makeId, modelId, 2020, "ABC123", null, null, null, null));

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
            new CreateCarRequest(customerId, makeId, otherModelId, 2020, "ABC123", null, null, null, null));

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
            new CreateCarRequest(customerId, Guid.NewGuid(), modelId, 2020, "ABC123", null, null, null, null));

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
        await service.CreateAsync(new CreateCarRequest(customerA, makeId, modelId, 2021, "A1", null, null, null, null));
        await service.CreateAsync(new CreateCarRequest(customerA, makeId, modelId, 2018, "A2", null, null, null, null));
        await service.CreateAsync(new CreateCarRequest(customerB, makeId, modelId, 2022, "B1", null, null, null, null));

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
            new CreateCarRequest(customerId, makeId, modelId, 2010, "OLD1", "v", "Blue", "Diesel", 100));

        var (updated, error) = await service.UpdateAsync(created!.Id,
            new UpdateCarRequest(makeId, modelId, 2011, "NEW1", null, "Green", null, 200));

        Assert.Equal(CarWriteError.None, error);
        Assert.NotNull(updated);
        Assert.Equal(2011, updated!.Year);
        Assert.Equal("Green", updated.Color);
        Assert.Null(updated.Vin);
        Assert.Equal(200, updated.Odometer);
        Assert.NotNull(updated.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFound_WhenMissing()
    {
        await using var db = CreateContext();
        var (makeId, modelId) = await SeedMakeModelAsync(db);
        var service = new CarService(db);

        var (car, error) = await service.UpdateAsync(Guid.NewGuid(),
            new UpdateCarRequest(makeId, modelId, 2020, "R", null, null, null, null));

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
            new CreateCarRequest(customerId, makeId, modelId, 2020, "TMP1", null, null, null, null));

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
}
