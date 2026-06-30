using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class JobServiceCatalogServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task CreateAsync_PersistsService_AndReturnsDto()
    {
        await using var db = CreateContext();
        var catalog = new JobServiceCatalogService(db);

        var result = await catalog.CreateAsync(new CreateJobServiceRequest("Oil change", "Includes filter", 89.00m, true));

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Oil change", result.Name);
        Assert.True(result.IsActive);
        Assert.Equal(1, await db.JobServices.CountAsync());
    }

    [Fact]
    public async Task GetAllAsync_ActiveOnly_ExcludesInactive()
    {
        await using var db = CreateContext();
        var catalog = new JobServiceCatalogService(db);
        await catalog.CreateAsync(new CreateJobServiceRequest("General service", null, 250m, true));
        await catalog.CreateAsync(new CreateJobServiceRequest("Retired service", null, 1m, false));

        var all = await catalog.GetAllAsync();
        var active = await catalog.GetAllAsync(activeOnly: true);

        Assert.Equal(2, all.Count);
        Assert.Single(active);
        Assert.Equal("General service", active[0].Name);
    }

    [Fact]
    public async Task UpdateAsync_ModifiesFields_AndStampsUpdatedAt()
    {
        await using var db = CreateContext();
        var catalog = new JobServiceCatalogService(db);
        var created = await catalog.CreateAsync(new CreateJobServiceRequest("Old", null, 1m, true));

        var updated = await catalog.UpdateAsync(created.Id, new UpdateJobServiceRequest("New", "desc", 2m, false));

        Assert.NotNull(updated);
        Assert.Equal("New", updated!.Name);
        Assert.False(updated.IsActive);
        Assert.NotNull(updated.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenMissing()
    {
        await using var db = CreateContext();
        var catalog = new JobServiceCatalogService(db);

        Assert.Null(await catalog.UpdateAsync(Guid.NewGuid(), new UpdateJobServiceRequest("X", null, 1m, true)));
    }

    [Fact]
    public async Task DeleteAsync_RemovesService_AndReturnsTrue()
    {
        await using var db = CreateContext();
        var catalog = new JobServiceCatalogService(db);
        var created = await catalog.CreateAsync(new CreateJobServiceRequest("Temp", null, 1m, true));

        Assert.True(await catalog.DeleteAsync(created.Id));
        Assert.Equal(0, await db.JobServices.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenMissing()
    {
        await using var db = CreateContext();
        var catalog = new JobServiceCatalogService(db);

        Assert.False(await catalog.DeleteAsync(Guid.NewGuid()));
    }
}
