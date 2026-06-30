using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface ICarMakeService
{
    Task<IReadOnlyList<CarMakeDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<CarMakeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CarMakeDto> CreateAsync(CreateCarMakeRequest request, CancellationToken cancellationToken = default);

    Task<CarMakeDto?> UpdateAsync(Guid id, UpdateCarMakeRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
