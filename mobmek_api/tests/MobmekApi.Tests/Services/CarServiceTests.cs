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

    [Fact]
    public async Task CreateAsync_PersistsCar_AndReturnsDto()
    {
        await using var db = CreateContext();
        var customerId = await SeedCustomerAsync(db);
        var service = new CarService(db);

        var result = await service.CreateAsync(
            new CreateCarRequest(customerId, "Toyota", "Corolla", 2020, "ABC123", "VIN123", "Red", "Petrol", 50000));

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result!.Id);
        Assert.Equal(customerId, result.CustomerId);
        Assert.Equal("Toyota", result.Make);
        Assert.Equal(1, await db.Cars.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_ReturnsNull_WhenCustomerDoesNotExist()
    {
        await using var db = CreateContext();
        var service = new CarService(db);

        var result = await service.CreateAsync(
            new CreateCarRequest(Guid.NewGuid(), "Toyota", "Corolla", 2020, "ABC123", null, null, null, null));

        Assert.Null(result);
        Assert.Equal(0, await db.Cars.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_AllowsNullOptionalFields()
    {
        await using var db = CreateContext();
        var customerId = await SeedCustomerAsync(db);
        var service = new CarService(db);

        var result = await service.CreateAsync(
            new CreateCarRequest(customerId, "Mazda", "3", 2019, "XYZ789", null, null, null, null));

        Assert.NotNull(result);
        Assert.Null(result!.Vin);
        Assert.Null(result.Color);
        Assert.Null(result.EngineType);
        Assert.Null(result.Odometer);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByCustomerId()
    {
        await using var db = CreateContext();
        var service = new CarService(db);
        var customerA = await SeedCustomerAsync(db);
        var customerB = await SeedCustomerAsync(db);
        await service.CreateAsync(new CreateCarRequest(customerA, "Honda", "Civic", 2021, "A1", null, null, null, null));
        await service.CreateAsync(new CreateCarRequest(customerA, "Ford", "Focus", 2018, "A2", null, null, null, null));
        await service.CreateAsync(new CreateCarRequest(customerB, "Kia", "Rio", 2022, "B1", null, null, null, null));

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
        var service = new CarService(db);
        var created = await service.CreateAsync(
            new CreateCarRequest(customerId, "Old", "Model", 2010, "OLD1", "v", "Blue", "Diesel", 100));

        var updated = await service.UpdateAsync(
            created!.Id, new UpdateCarRequest("New", "Model", 2011, "NEW1", null, "Green", null, 200));

        Assert.NotNull(updated);
        Assert.Equal("New", updated!.Make);
        Assert.Equal(2011, updated.Year);
        Assert.Equal("Green", updated.Color);
        Assert.Null(updated.Vin);
        Assert.Equal(200, updated.Odometer);
        Assert.NotNull(updated.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenMissing()
    {
        await using var db = CreateContext();
        var service = new CarService(db);

        var result = await service.UpdateAsync(
            Guid.NewGuid(), new UpdateCarRequest("X", "Y", 2020, "R", null, null, null, null));

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_RemovesCar_AndReturnsTrue()
    {
        await using var db = CreateContext();
        var customerId = await SeedCustomerAsync(db);
        var service = new CarService(db);
        var created = await service.CreateAsync(
            new CreateCarRequest(customerId, "Temp", "Car", 2020, "TMP1", null, null, null, null));

        var deleted = await service.DeleteAsync(created!.Id);

        Assert.True(deleted);
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
