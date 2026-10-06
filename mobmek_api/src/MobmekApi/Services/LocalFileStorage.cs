namespace MobmekApi.Services;

/// <summary>
/// <see cref="IFileStorage"/> backed by a directory on local disk — the development default,
/// so nothing local needs AWS credentials. Keys come from <see cref="StorageKeys"/>, so they
/// are byte-for-byte the object keys <see cref="S3FileStorage"/> uses in production.
/// </summary>
public class LocalFileStorage(string rootPath) : IFileStorage
{
    // contentType is ignored: a file on disk carries no content-type metadata, and callers keep
    // their own copy in the database. S3FileStorage does record it on the object.
    public async Task<string> SaveAsync(
        Stream content, string fileName, string? contentType = null, CancellationToken cancellationToken = default)
    {
        var key = StorageKeys.Create(fileName);
        var fullPath = ResolveSafe(key);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var file = File.Create(fullPath);
        await content.CopyToAsync(file, cancellationToken);

        return key;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafe(storageKey);
        return Task.FromResult<Stream?>(File.Exists(fullPath) ? File.OpenRead(fullPath) : null);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafe(storageKey);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    // Rejects keys that would escape the storage root (path traversal).
    private string ResolveSafe(string storageKey)
    {
        var root = Path.GetFullPath(rootPath);
        var fullPath = Path.GetFullPath(Path.Combine(root, storageKey));
        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Storage key '{storageKey}' resolves outside the storage root.", nameof(storageKey));
        }

        return fullPath;
    }
}
