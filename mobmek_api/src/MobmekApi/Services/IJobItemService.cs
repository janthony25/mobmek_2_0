using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface IJobItemService
{
    /// <summary>Lists job items, optionally filtered to a single job.</summary>
    Task<IReadOnlyList<JobItemDto>> GetAllAsync(Guid? jobId = null, CancellationToken cancellationToken = default);

    Task<JobItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates an item. Returns <c>null</c> when the referenced job does not exist.</summary>
    Task<JobItemDto?> CreateAsync(CreateJobItemRequest request, CancellationToken cancellationToken = default);

    Task<JobItemDto?> UpdateAsync(Guid id, UpdateJobItemRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
