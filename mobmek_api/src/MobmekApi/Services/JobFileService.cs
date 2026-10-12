using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

/// <summary>
/// Job files. Rows live in the database; the file bytes live behind
/// <see cref="IFileStorage"/>, so the two are written and deleted together here.
/// </summary>
public class JobFileService(AppDbContext db, IFileStorage fileStorage) : IJobFileService
{
    public async Task<IReadOnlyList<JobFileDto>> GetAllAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        await db.JobFiles.AsNoTracking()
            .Where(f => f.JobId == jobId)
            .OrderBy(f => f.CreatedAtUtc)
            .Select(f => new JobFileDto(f.Id, f.JobId, f.FileName, f.ContentType, f.SizeBytes, f.CreatedAtUtc))
            .ToListAsync(cancellationToken);

    public async Task<JobFileDto?> AddAsync(
        Guid jobId, Stream content, string fileName, string contentType, long sizeBytes, CancellationToken cancellationToken = default)
    {
        if (!await db.Jobs.AnyAsync(j => j.Id == jobId, cancellationToken))
        {
            return null;
        }

        var storageKey = await fileStorage.SaveAsync(content, fileName, contentType, cancellationToken);
        var file = new JobFile
        {
            JobId = jobId,
            FileName = fileName,
            ContentType = contentType,
            StorageKey = storageKey,
            SizeBytes = sizeBytes,
        };

        db.JobFiles.Add(file);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(file);
    }

    public async Task<(JobFileDto File, Stream Content)?> GetContentAsync(
        Guid jobId, Guid fileId, CancellationToken cancellationToken = default)
    {
        var file = await db.JobFiles.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fileId && f.JobId == jobId, cancellationToken);
        if (file is null)
        {
            return null;
        }

        var content = await fileStorage.OpenReadAsync(file.StorageKey, cancellationToken);
        return content is null ? null : (ToDto(file), content);
    }

    public async Task<bool> DeleteAsync(Guid jobId, Guid fileId, CancellationToken cancellationToken = default)
    {
        var file = await db.JobFiles.FirstOrDefaultAsync(f => f.Id == fileId && f.JobId == jobId, cancellationToken);
        if (file is null)
        {
            return false;
        }

        await fileStorage.DeleteAsync(file.StorageKey, cancellationToken);
        db.JobFiles.Remove(file);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static JobFileDto ToDto(JobFile f) =>
        new(f.Id, f.JobId, f.FileName, f.ContentType, f.SizeBytes, f.CreatedAtUtc);
}
