using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class EmployeeTitleServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task CreateAsync_PersistsTitle_AndReturnsDto()
    {
        await using var db = CreateContext();
        var service = new EmployeeTitleService(db);

        var result = await service.CreateAsync(new CreateEmployeeTitleRequest("Mechanic"));

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Mechanic", result.Name);
        Assert.Equal(1, await db.EmployeeTitles.CountAsync());
    }

    [Fact]
    public async Task GetAllAsync_ReturnsTitlesOrderedByName()
    {
        await using var db = CreateContext();
        var service = new EmployeeTitleService(db);
        await service.CreateAsync(new CreateEmployeeTitleRequest("Manager"));
        await service.CreateAsync(new CreateEmployeeTitleRequest("Apprentice"));

        var result = await service.GetAllAsync();

        Assert.Collection(result,
            t => Assert.Equal("Apprentice", t.Name),
            t => Assert.Equal("Manager", t.Name));
    }

    [Fact]
    public async Task UpdateAsync_ModifiesName_AndStampsUpdatedAt()
    {
        await using var db = CreateContext();
        var service = new EmployeeTitleService(db);
        var created = await service.CreateAsync(new CreateEmployeeTitleRequest("Old"));

        var updated = await service.UpdateAsync(created.Id, new UpdateEmployeeTitleRequest("New"));

        Assert.NotNull(updated);
        Assert.Equal("New", updated!.Name);
        Assert.NotNull(updated.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenMissing()
    {
        await using var db = CreateContext();
        var service = new EmployeeTitleService(db);

        Assert.Null(await service.UpdateAsync(Guid.NewGuid(), new UpdateEmployeeTitleRequest("X")));
    }

    [Fact]
    public async Task DeleteAsync_RemovesTitle_AndReturnsTrue()
    {
        await using var db = CreateContext();
        var service = new EmployeeTitleService(db);
        var created = await service.CreateAsync(new CreateEmployeeTitleRequest("Temp"));

        Assert.True(await service.DeleteAsync(created.Id));
        Assert.Equal(0, await db.EmployeeTitles.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenMissing()
    {
        await using var db = CreateContext();
        var service = new EmployeeTitleService(db);

        Assert.False(await service.DeleteAsync(Guid.NewGuid()));
    }
}
