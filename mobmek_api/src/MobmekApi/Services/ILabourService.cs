using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface ILabourService
{
    /// <summary>Lists labour lines, optionally filtered to a single job.</summary>
    Task<IReadOnlyList<LabourDto>> GetAllAsync(Guid? jobId = null, CancellationToken cancellationToken = default);

    Task<LabourDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a labour line. Returns <c>null</c> when the referenced job does not exist.</summary>
    Task<LabourDto?> CreateAsync(CreateLabourRequest request, CancellationToken cancellationToken = default);

    Task<LabourDto?> UpdateAsync(Guid id, UpdateLabourRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
