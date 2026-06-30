using MobmekApi.DTOs;

namespace MobmekApi.Services;

/// <summary>Outcome of attaching a catalog service to a job.</summary>
public enum JobServiceLineWriteError
{
    None,
    JobNotFound,
    ServiceNotFound,
    ServiceInactive,
}

public interface IJobServiceLineService
{
    /// <summary>Lists the service lines belonging to a job.</summary>
    Task<IReadOnlyList<JobServiceLineDto>> GetAllAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Returns one service line, only if it belongs to <paramref name="jobId"/>.</summary>
    Task<JobServiceLineDto?> GetByIdAsync(Guid jobId, Guid id, CancellationToken cancellationToken = default);

    Task<(JobServiceLineDto? Line, JobServiceLineWriteError Error)> CreateAsync(Guid jobId, CreateJobServiceLineRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates a service line (quantity), only if it belongs to <paramref name="jobId"/>.</summary>
    Task<JobServiceLineDto?> UpdateAsync(Guid jobId, Guid id, UpdateJobServiceLineRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes a service line, only if it belongs to <paramref name="jobId"/>.</summary>
    Task<bool> DeleteAsync(Guid jobId, Guid id, CancellationToken cancellationToken = default);
}
