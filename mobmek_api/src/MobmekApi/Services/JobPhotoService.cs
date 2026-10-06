using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

/// <summary>
/// Job photos. Rows live in the database; the image bytes live behind
/// <see cref="IFileStorage"/>, so the two are written and deleted together here.
/// </summary>
public class JobPhotoService(AppDbContext db, IFileStorage fileStorage) : IJobPhotoService
{
    public async Task<IReadOnlyList<JobPhotoDto>> GetAllAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        await db.JobPhotos.AsNoTracking()
            .Where(p => p.JobId == jobId)
            .OrderBy(p => p.CreatedAtUtc)
            .Select(p => new JobPhotoDto(p.Id, p.JobId, p.FileName, p.ContentType, p.SizeBytes, p.CreatedAtUtc))
            .ToListAsync(cancellationToken);

    public async Task<JobPhotoDto?> AddAsync(
        Guid jobId, Stream content, string fileName, string contentType, long sizeBytes, CancellationToken cancellationToken = default)
    {
        if (!await db.Jobs.AnyAsync(j => j.Id == jobId, cancellationToken))
        {
            return null;
        }

        var storageKey = await fileStorage.SaveAsync(content, fileName, contentType, cancellationToken);
        var photo = new JobPhoto
        {
            JobId = jobId,
            FileName = fileName,
            ContentType = contentType,
            StorageKey = storageKey,
            SizeBytes = sizeBytes,
        };

        db.JobPhotos.Add(photo);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(photo);
    }

    public async Task<(JobPhotoDto Photo, Stream Content)?> GetContentAsync(
        Guid jobId, Guid photoId, CancellationToken cancellationToken = default)
    {
        var photo = await db.JobPhotos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == photoId && p.JobId == jobId, cancellationToken);
        if (photo is null)
        {
            return null;
        }

        var content = await fileStorage.OpenReadAsync(photo.StorageKey, cancellationToken);
        return content is null ? null : (ToDto(photo), content);
    }

    public async Task<bool> DeleteAsync(Guid jobId, Guid photoId, CancellationToken cancellationToken = default)
    {
        var photo = await db.JobPhotos.FirstOrDefaultAsync(p => p.Id == photoId && p.JobId == jobId, cancellationToken);
        if (photo is null)
        {
            return false;
        }

        await fileStorage.DeleteAsync(photo.StorageKey, cancellationToken);
        db.JobPhotos.Remove(photo);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static JobPhotoDto ToDto(JobPhoto p) =>
        new(p.Id, p.JobId, p.FileName, p.ContentType, p.SizeBytes, p.CreatedAtUtc);
}
