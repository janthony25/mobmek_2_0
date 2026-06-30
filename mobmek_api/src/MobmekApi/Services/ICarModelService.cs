using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface ICarModelService
{
    /// <summary>Lists models, optionally filtered to a single make.</summary>
    Task<IReadOnlyList<CarModelDto>> GetAllAsync(Guid? makeId = null, CancellationToken cancellationToken = default);

    Task<CarModelDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a model. Returns <c>null</c> when the referenced make does not exist.</summary>
    Task<CarModelDto?> CreateAsync(CreateCarModelRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates a model. Returns <c>null</c> when the model is missing or the make does not exist.</summary>
    Task<(CarModelDto? Model, bool MakeMissing)> UpdateAsync(Guid id, UpdateCarModelRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
