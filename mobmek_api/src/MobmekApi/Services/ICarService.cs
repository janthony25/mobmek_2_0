using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface ICarService
{
    /// <summary>Lists cars, optionally filtered to a single customer.</summary>
    Task<IReadOnlyList<CarDto>> GetAllAsync(Guid? customerId = null, CancellationToken cancellationToken = default);

    Task<CarDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a car. Returns <c>null</c> when the referenced customer does not exist.</summary>
    Task<CarDto?> CreateAsync(CreateCarRequest request, CancellationToken cancellationToken = default);

    Task<CarDto?> UpdateAsync(Guid id, UpdateCarRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
