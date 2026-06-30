using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class ProductServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task CreateAsync_PersistsProduct_AndReturnsDto()
    {
        await using var db = CreateContext();
        var service = new ProductService(db);

        var result = await service.CreateAsync(
            new CreateProductRequest("Widget", "A useful widget", 19.99m, 5));

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Widget", result.Name);
        Assert.Equal(1, await db.Products.CountAsync());
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenMissing()
    {
        await using var db = CreateContext();
        var service = new ProductService(db);

        var result = await service.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsProductsOrderedByName()
    {
        await using var db = CreateContext();
        var service = new ProductService(db);
        await service.CreateAsync(new CreateProductRequest("Zeta", null, 1m, 1));
        await service.CreateAsync(new CreateProductRequest("Alpha", null, 1m, 1));

        var result = await service.GetAllAsync();

        Assert.Collection(result,
            p => Assert.Equal("Alpha", p.Name),
            p => Assert.Equal("Zeta", p.Name));
    }

    [Fact]
    public async Task UpdateAsync_ModifiesFields_AndStampsUpdatedAt()
    {
        await using var db = CreateContext();
        var service = new ProductService(db);
        var created = await service.CreateAsync(new CreateProductRequest("Old", null, 1m, 1));

        var updated = await service.UpdateAsync(
            created.Id, new UpdateProductRequest("New", "changed", 2m, 10));

        Assert.NotNull(updated);
        Assert.Equal("New", updated!.Name);
        Assert.Equal(10, updated.StockQuantity);
        Assert.NotNull(updated.UpdatedAtUtc);
    }

    [Fact]
    public async Task DeleteAsync_RemovesProduct_AndReturnsTrue()
    {
        await using var db = CreateContext();
        var service = new ProductService(db);
        var created = await service.CreateAsync(new CreateProductRequest("Temp", null, 1m, 1));

        var deleted = await service.DeleteAsync(created.Id);

        Assert.True(deleted);
        Assert.Equal(0, await db.Products.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenMissing()
    {
        await using var db = CreateContext();
        var service = new ProductService(db);

        var deleted = await service.DeleteAsync(Guid.NewGuid());

        Assert.False(deleted);
    }
}
