using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface IJobFileService
{
    /// <summary>Lists the files on a job, oldest first.</summary>
    Task<IReadOnlyList<JobFileDto>> GetAllAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Stores a file against a job. Returns <c>null</c> when the job does not exist.</summary>
    Task<JobFileDto?> AddAsync(
        Guid jobId, Stream content, string fileName, string contentType, long sizeBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens one file's bytes, only if it belongs to <paramref name="jobId"/>.
    /// Returns <c>null</c> when the file row or its stored content is missing.
    /// </summary>
    Task<(JobFileDto File, Stream Content)?> GetContentAsync(Guid jobId, Guid fileId, CancellationToken cancellationToken = default);

    /// <summary>Deletes a file and its stored content, only if it belongs to <paramref name="jobId"/>.</summary>
    Task<bool> DeleteAsync(Guid jobId, Guid fileId, CancellationToken cancellationToken = default);
}
