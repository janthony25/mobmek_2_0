namespace MobmekApi.Services;

/// <summary>
/// Provider-agnostic blob storage for uploaded files (transaction receipts etc.).
/// The local-disk implementation is used today; an S3 implementation can replace it
/// later without touching callers — they only ever hold the returned storage key.
/// </summary>
public interface IFileStorage
{
    /// <summary>Stores the content and returns the storage key used to read/delete it later.</summary>
    Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken = default);

    /// <summary>Opens the stored content for reading, or returns <c>null</c> when the key doesn't exist.</summary>
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);

    /// <summary>Deletes the stored content; a missing key is not an error.</summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
