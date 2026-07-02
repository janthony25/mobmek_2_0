namespace MobmekApi.Services;

/// <summary>
/// <see cref="IFileStorage"/> backed by a directory on local disk. Keys are
/// "yyyy/MM/{guid}{ext}" relative paths, so they translate directly to S3 object keys
/// when storage moves to the cloud.
/// </summary>
public class LocalFileStorage(string rootPath) : IFileStorage
{
    public async Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        // Only the extension of the original name is trusted; the key itself is generated.
        var extension = Path.GetExtension(fileName);
        if (extension.Length > 10 || extension.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            extension = string.Empty;
        }

        var key = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{extension}";
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
