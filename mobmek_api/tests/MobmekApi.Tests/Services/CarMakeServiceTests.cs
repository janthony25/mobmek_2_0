using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class CarMakeServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task CreateAsync_PersistsMake_AndReturnsDto()
    {
        await using var db = CreateContext();
        var service = new CarMakeService(db);

        var result = await service.CreateAsync(new CreateCarMakeRequest("BMW"));

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("BMW", result.Name);
        Assert.Equal(1, await db.CarMakes.CountAsync());
    }

    [Fact]
    public async Task GetAllAsync_ReturnsMakesOrderedByName()
    {
        await using var db = CreateContext();
        var service = new CarMakeService(db);
        await service.CreateAsync(new CreateCarMakeRequest("Toyota"));
        await service.CreateAsync(new CreateCarMakeRequest("BMW"));

        var result = await service.GetAllAsync();

        Assert.Collection(result,
            m => Assert.Equal("BMW", m.Name),
            m => Assert.Equal("Toyota", m.Name));
    }

    [Fact]
    public async Task UpdateAsync_ModifiesName_AndStampsUpdatedAt()
    {
        await using var db = CreateContext();
        var service = new CarMakeService(db);
        var created = await service.CreateAsync(new CreateCarMakeRequest("Old"));

        var updated = await service.UpdateAsync(created.Id, new UpdateCarMakeRequest("New"));

        Assert.NotNull(updated);
        Assert.Equal("New", updated!.Name);
        Assert.NotNull(updated.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenMissing()
    {
        await using var db = CreateContext();
        var service = new CarMakeService(db);

        Assert.Null(await service.UpdateAsync(Guid.NewGuid(), new UpdateCarMakeRequest("X")));
    }

    [Fact]
    public async Task DeleteAsync_RemovesMake_AndReturnsTrue()
    {
        await using var db = CreateContext();
        var service = new CarMakeService(db);
        var created = await service.CreateAsync(new CreateCarMakeRequest("Temp"));

        Assert.True(await service.DeleteAsync(created.Id));
        Assert.Equal(0, await db.CarMakes.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenMissing()
    {
        await using var db = CreateContext();
        var service = new CarMakeService(db);

        Assert.False(await service.DeleteAsync(Guid.NewGuid()));
    }
}
