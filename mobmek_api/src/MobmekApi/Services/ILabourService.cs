using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface ILabourService
{
    /// <summary>Lists the labour lines belonging to a job.</summary>
    Task<IReadOnlyList<LabourDto>> GetAllAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Returns one labour line, only if it belongs to <paramref name="jobId"/>.</summary>
    Task<LabourDto?> GetByIdAsync(Guid jobId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a labour line on a job. Returns <c>null</c> when the job does not exist.</summary>
    Task<LabourDto?> CreateAsync(Guid jobId, CreateLabourRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates a labour line, only if it belongs to <paramref name="jobId"/>. Returns <c>null</c> when not found.</summary>
    Task<LabourDto?> UpdateAsync(Guid jobId, Guid id, UpdateLabourRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes a labour line, only if it belongs to <paramref name="jobId"/>.</summary>
    Task<bool> DeleteAsync(Guid jobId, Guid id, CancellationToken cancellationToken = default);
}
