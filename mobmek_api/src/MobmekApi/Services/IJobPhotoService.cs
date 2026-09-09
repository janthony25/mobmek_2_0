using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface IJobPhotoService
{
    /// <summary>Lists the photos on a job, oldest first.</summary>
    Task<IReadOnlyList<JobPhotoDto>> GetAllAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Stores a photo against a job. Returns <c>null</c> when the job does not exist.</summary>
    Task<JobPhotoDto?> AddAsync(
        Guid jobId, Stream content, string fileName, string contentType, long sizeBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens one photo's image bytes, only if it belongs to <paramref name="jobId"/>.
    /// Returns <c>null</c> when the photo row or its stored file is missing.
    /// </summary>
    Task<(JobPhotoDto Photo, Stream Content)?> GetContentAsync(Guid jobId, Guid photoId, CancellationToken cancellationToken = default);

    /// <summary>Deletes a photo and its stored file, only if it belongs to <paramref name="jobId"/>.</summary>
    Task<bool> DeleteAsync(Guid jobId, Guid photoId, CancellationToken cancellationToken = default);
}
