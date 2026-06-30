using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class EmploymentTypeServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task CreateAsync_PersistsType_AndReturnsDto()
    {
        await using var db = CreateContext();
        var service = new EmploymentTypeService(db);

        var result = await service.CreateAsync(new CreateEmploymentTypeRequest("Full-time"));

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Full-time", result.Name);
        Assert.Equal(1, await db.EmploymentTypes.CountAsync());
    }

    [Fact]
    public async Task GetAllAsync_ReturnsTypesOrderedByName()
    {
        await using var db = CreateContext();
        var service = new EmploymentTypeService(db);
        await service.CreateAsync(new CreateEmploymentTypeRequest("Part-time"));
        await service.CreateAsync(new CreateEmploymentTypeRequest("Casual"));

        var result = await service.GetAllAsync();

        Assert.Collection(result,
            t => Assert.Equal("Casual", t.Name),
            t => Assert.Equal("Part-time", t.Name));
    }

    [Fact]
    public async Task UpdateAsync_ModifiesName_AndStampsUpdatedAt()
    {
        await using var db = CreateContext();
        var service = new EmploymentTypeService(db);
        var created = await service.CreateAsync(new CreateEmploymentTypeRequest("Old"));

        var updated = await service.UpdateAsync(created.Id, new UpdateEmploymentTypeRequest("New"));

        Assert.NotNull(updated);
        Assert.Equal("New", updated!.Name);
        Assert.NotNull(updated.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenMissing()
    {
        await using var db = CreateContext();
        var service = new EmploymentTypeService(db);

        Assert.Null(await service.UpdateAsync(Guid.NewGuid(), new UpdateEmploymentTypeRequest("X")));
    }

    [Fact]
    public async Task DeleteAsync_RemovesType_AndReturnsTrue()
    {
        await using var db = CreateContext();
        var service = new EmploymentTypeService(db);
        var created = await service.CreateAsync(new CreateEmploymentTypeRequest("Temp"));

        Assert.True(await service.DeleteAsync(created.Id));
        Assert.Equal(0, await db.EmploymentTypes.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenMissing()
    {
        await using var db = CreateContext();
        var service = new EmploymentTypeService(db);

        Assert.False(await service.DeleteAsync(Guid.NewGuid()));
    }
}
