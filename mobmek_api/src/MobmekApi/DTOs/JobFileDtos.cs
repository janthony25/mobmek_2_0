namespace MobmekApi.DTOs;

/// <summary>A file attached to a job (metadata only). Content is fetched from the download endpoint.</summary>
public record JobFileDto(
    Guid Id,
    Guid JobId,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime CreatedAtUtc);
