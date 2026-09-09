namespace MobmekApi.Entities;

/// <summary>
/// A photo taken or uploaded against a job (damage, parts, before/after). The image bytes
/// live behind <see cref="Services.IFileStorage"/> (local disk now, S3 later);
/// <see cref="StorageKey"/> is the provider-agnostic handle to them.
/// </summary>
public class JobPhoto : BaseEntity
{
    public Guid JobId { get; set; }

    public Job? Job { get; set; }

    /// <summary>Original file name as uploaded, used for download.</summary>
    public required string FileName { get; set; }

    public required string ContentType { get; set; }

    /// <summary>Provider-agnostic storage handle (a relative path locally; an object key on S3).</summary>
    public required string StorageKey { get; set; }

    public long SizeBytes { get; set; }
}
