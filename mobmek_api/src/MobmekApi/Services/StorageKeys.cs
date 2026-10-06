namespace MobmekApi.Services;

/// <summary>
/// Builds the storage keys every <see cref="IFileStorage"/> implementation uses. Kept in one
/// place so a file saved to local disk and the same file saved to S3 are addressed identically:
/// the "yyyy/MM/{guid}{ext}" shape is a valid relative path and a valid S3 object key at once,
/// which is what lets the backend be swapped without rewriting keys already in the database.
/// </summary>
public static class StorageKeys
{
    /// <summary>Longest extension carried over from an uploaded name; anything longer is dropped.</summary>
    private const int MaxExtensionLength = 10;

    /// <summary>
    /// Builds a fresh key for <paramref name="fileName"/>. Only the extension of the caller's
    /// name is reused, and only when it is short and free of path characters — the rest of the
    /// key is generated, so an uploaded name can never influence where the file lands.
    /// </summary>
    public static string Create(string? fileName, DateTime? utcNow = null)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty);
        if (extension.Length > MaxExtensionLength || extension.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            extension = string.Empty;
        }

        return $"{utcNow ?? DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{extension}";
    }
}
