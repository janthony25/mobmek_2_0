using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface IJobItemService
{
    /// <summary>Lists the items belonging to a job.</summary>
    Task<IReadOnlyList<JobItemDto>> GetAllAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Returns one item, only if it belongs to <paramref name="jobId"/>.</summary>
    Task<JobItemDto?> GetByIdAsync(Guid jobId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates an item on a job. Returns <c>null</c> when the job does not exist.</summary>
    Task<JobItemDto?> CreateAsync(Guid jobId, CreateJobItemRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates an item, only if it belongs to <paramref name="jobId"/>. Returns <c>null</c> when not found.</summary>
    Task<JobItemDto?> UpdateAsync(Guid jobId, Guid id, UpdateJobItemRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes an item, only if it belongs to <paramref name="jobId"/>.</summary>
    Task<bool> DeleteAsync(Guid jobId, Guid id, CancellationToken cancellationToken = default);
}
