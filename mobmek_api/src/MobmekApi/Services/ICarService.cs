using MobmekApi.DTOs;

namespace MobmekApi.Services;

/// <summary>Outcome of a car write that depends on referenced records.</summary>
public enum CarWriteError
{
    None,
    NotFound,
    CustomerNotFound,
    MakeNotFound,
    ModelNotFound,
    ModelNotInMake,
}

public interface ICarService
{
    /// <summary>Lists cars, optionally filtered to a single customer.</summary>
    Task<IReadOnlyList<CarDto>> GetAllAsync(Guid? customerId = null, CancellationToken cancellationToken = default);

    Task<CarDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(CarDto? Car, CarWriteError Error)> CreateAsync(CreateCarRequest request, CancellationToken cancellationToken = default);

    Task<(CarDto? Car, CarWriteError Error)> UpdateAsync(Guid id, UpdateCarRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
