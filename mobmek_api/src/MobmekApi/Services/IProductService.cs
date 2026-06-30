using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface IProductService
{
    Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);

    Task<ProductDto?> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
