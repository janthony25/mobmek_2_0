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
    /// <summary>Lists service lines, optionally filtered to a single job.</summary>
    Task<IReadOnlyList<JobServiceLineDto>> GetAllAsync(Guid? jobId = null, CancellationToken cancellationToken = default);

    Task<JobServiceLineDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(JobServiceLineDto? Line, JobServiceLineWriteError Error)> CreateAsync(CreateJobServiceLineRequest request, CancellationToken cancellationToken = default);

    Task<JobServiceLineDto?> UpdateAsync(Guid id, UpdateJobServiceLineRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
