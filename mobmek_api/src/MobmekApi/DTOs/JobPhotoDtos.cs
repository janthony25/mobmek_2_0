namespace MobmekApi.DTOs;

/// <summary>
/// A photo attached to a job (metadata only; the image bytes are downloaded separately
/// from <c>GET api/jobs/{jobId}/photos/{id}</c>).
/// </summary>
public record JobPhotoDto(
    Guid Id,
    Guid JobId,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime CreatedAtUtc);
