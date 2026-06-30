using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class CarModelServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task CreateAsync_PersistsModel_AndResolvesMakeName()
    {
        await using var db = CreateContext();
        var make = await new CarMakeService(db).CreateAsync(new CreateCarMakeRequest("BMW"));
        var service = new CarModelService(db);

        var model = await service.CreateAsync(new CreateCarModelRequest(make.Id, "Z3"));

        Assert.NotNull(model);
        Assert.Equal("Z3", model!.Name);
        Assert.Equal("BMW", model.CarMakeName);
        Assert.Equal(make.Id, model.CarMakeId);
    }

    [Fact]
    public async Task CreateAsync_ReturnsNull_WhenMakeMissing()
    {
        await using var db = CreateContext();
        var service = new CarModelService(db);

        var model = await service.CreateAsync(new CreateCarModelRequest(Guid.NewGuid(), "Ghost"));

        Assert.Null(model);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByMake()
    {
        await using var db = CreateContext();
        var makeService = new CarMakeService(db);
        var bmw = await makeService.CreateAsync(new CreateCarMakeRequest("BMW"));
        var toyota = await makeService.CreateAsync(new CreateCarMakeRequest("Toyota"));
        var service = new CarModelService(db);
        await service.CreateAsync(new CreateCarModelRequest(bmw.Id, "Z3"));
        await service.CreateAsync(new CreateCarModelRequest(bmw.Id, "X5"));
        await service.CreateAsync(new CreateCarModelRequest(toyota.Id, "Prius"));

        var bmwModels = await service.GetAllAsync(bmw.Id);

        Assert.Equal(2, bmwModels.Count);
        Assert.All(bmwModels, m => Assert.Equal(bmw.Id, m.CarMakeId));
    }

    [Fact]
    public async Task UpdateAsync_ReturnsMakeMissing_WhenNewMakeInvalid()
    {
        await using var db = CreateContext();
        var make = await new CarMakeService(db).CreateAsync(new CreateCarMakeRequest("BMW"));
        var service = new CarModelService(db);
        var model = await service.CreateAsync(new CreateCarModelRequest(make.Id, "Z3"));

        var (updated, makeMissing) = await service.UpdateAsync(model!.Id, new UpdateCarModelRequest(Guid.NewGuid(), "Z4"));

        Assert.Null(updated);
        Assert.True(makeMissing);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFound_WhenModelMissing()
    {
        await using var db = CreateContext();
        var make = await new CarMakeService(db).CreateAsync(new CreateCarMakeRequest("BMW"));
        var service = new CarModelService(db);

        var (updated, makeMissing) = await service.UpdateAsync(Guid.NewGuid(), new UpdateCarModelRequest(make.Id, "X"));

        Assert.Null(updated);
        Assert.False(makeMissing);
    }

    [Fact]
    public async Task DeleteAsync_RemovesModel_AndReturnsTrue()
    {
        await using var db = CreateContext();
        var make = await new CarMakeService(db).CreateAsync(new CreateCarMakeRequest("BMW"));
        var service = new CarModelService(db);
        var model = await service.CreateAsync(new CreateCarModelRequest(make.Id, "Z3"));

        Assert.True(await service.DeleteAsync(model!.Id));
        Assert.Equal(0, await db.CarModels.CountAsync());
    }
}
