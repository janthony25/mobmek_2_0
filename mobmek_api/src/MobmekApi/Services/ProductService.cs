using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class ProductService(AppDbContext db) : IProductService
{
    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => ToDto(p))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return product is null ? null : ToDto(product);
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = new Product
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            StockQuantity = request.StockQuantity,
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(product);
    }

    public async Task<ProductDto?> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return null;
        }

        product.Name = request.Name;
        product.Description = request.Description;
        product.Price = request.Price;
        product.StockQuantity = request.StockQuantity;

        await db.SaveChangesAsync(cancellationToken);

        return ToDto(product);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return false;
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static ProductDto ToDto(Product p) =>
        new(p.Id, p.Name, p.Description, p.Price, p.StockQuantity, p.CreatedAtUtc, p.UpdatedAtUtc);
}
