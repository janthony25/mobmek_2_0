using System.Net;
using Amazon.S3;
using Amazon.S3.Model;

namespace MobmekApi.Services;

/// <summary>
/// <see cref="IFileStorage"/> backed by an S3 bucket — the production shape. Uploads kept on the
/// container's own filesystem are thrown away every time the container is rebuilt, which on a
/// single-box deployment is every code update; S3 outlives the instance entirely.
/// <para>
/// Keys are the same <see cref="StorageKeys"/> shape <see cref="LocalFileStorage"/> writes, so
/// keys already stored in the database stay valid across a move between the two backends.
/// </para>
/// </summary>
public class S3FileStorage(IAmazonS3 s3, string bucketName) : IFileStorage
{
    public async Task<string> SaveAsync(
        Stream content, string fileName, string? contentType = null, CancellationToken cancellationToken = default)
    {
        var key = StorageKeys.Create(fileName);
        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            InputStream = content,
            // Callers own the stream inside their own `await using`; letting the SDK close
            // it here would dispose it twice.
            AutoCloseStream = false,
        };

        // Stored on the object so it is correct for anything reading S3 directly — the console,
        // or a pre-signed URL handed to a browser. Reads through the API don't depend on it:
        // they serve the content type off the database row instead.
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            request.ContentType = contentType;
        }

        await s3.PutObjectAsync(request, cancellationToken);
        return key;
    }

    public async Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await s3.GetObjectAsync(bucketName, storageKey, cancellationToken);
            return response.ResponseStream;
        }
        // A missing object is an ordinary "not found", matching LocalFileStorage's null. S3 only
        // answers 404 here when the caller holds s3:ListBucket — without it a missing key comes
        // back as 403, which would be indistinguishable from a genuinely broken policy. The
        // instance role therefore grants ListBucket (see infrastructure/docs/phase-1-plan.md),
        // and a 403 is deliberately left to throw so a misconfigured deployment is loud rather
        // than silently reporting every photo as missing.
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    // DeleteObject is idempotent in S3 — removing a key that isn't there succeeds, which is
    // exactly the interface's "a missing key is not an error".
    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) =>
        s3.DeleteObjectAsync(bucketName, storageKey, cancellationToken);
}
