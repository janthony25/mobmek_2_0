using System.Linq.Expressions;
using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class JobServiceLineService(AppDbContext db, IJobService jobService) : IJobServiceLineService
{
    // Inline projection so EF resolves the catalog service name via a join.
    private static readonly Expression<Func<JobServiceLine, JobServiceLineDto>> ToDto =
        l => new JobServiceLineDto(
            l.Id, l.JobId, l.JobServiceId, l.JobService!.Name, l.UnitPrice, l.Quantity, l.LineTotal,
            l.CreatedAtUtc, l.UpdatedAtUtc);

    public async Task<IReadOnlyList<JobServiceLineDto>> GetAllAsync(Guid? jobId = null, CancellationToken cancellationToken = default)
    {
        var query = db.JobServiceLines.AsNoTracking();

        if (jobId is { } id)
        {
            query = query.Where(l => l.JobId == id);
        }

        return await query
            .OrderBy(l => l.CreatedAtUtc)
            .Select(ToDto)
            .ToListAsync(cancellationToken);
    }

    public async Task<JobServiceLineDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await db.JobServiceLines
            .AsNoTracking()
            .Where(l => l.Id == id)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(JobServiceLineDto? Line, JobServiceLineWriteError Error)> CreateAsync(CreateJobServiceLineRequest request, CancellationToken cancellationToken = default)
    {
        if (!await db.Jobs.AnyAsync(j => j.Id == request.JobId, cancellationToken))
        {
            return (null, JobServiceLineWriteError.JobNotFound);
        }

        var catalog = await db.JobServices.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.JobServiceId, cancellationToken);
        if (catalog is null)
        {
            return (null, JobServiceLineWriteError.ServiceNotFound);
        }

        if (!catalog.IsActive)
        {
            return (null, JobServiceLineWriteError.ServiceInactive);
        }

        var line = new JobServiceLine
        {
            JobId = request.JobId,
            JobServiceId = request.JobServiceId,
            UnitPrice = catalog.Price,   // snapshot
            Quantity = request.Quantity,
            LineTotal = Round(catalog.Price * request.Quantity),
        };

        db.JobServiceLines.Add(line);
        await db.SaveChangesAsync(cancellationToken);
        await jobService.RecalculateTotalsAsync(line.JobId, cancellationToken);

        return (await GetByIdAsync(line.Id, cancellationToken), JobServiceLineWriteError.None);
    }

    public async Task<JobServiceLineDto?> UpdateAsync(Guid id, UpdateJobServiceLineRequest request, CancellationToken cancellationToken = default)
    {
        var line = await db.JobServiceLines.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (line is null)
        {
            return null;
        }

        line.Quantity = request.Quantity;
        line.LineTotal = Round(line.UnitPrice * request.Quantity);

        await db.SaveChangesAsync(cancellationToken);
        await jobService.RecalculateTotalsAsync(line.JobId, cancellationToken);

        return await GetByIdAsync(line.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var line = await db.JobServiceLines.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (line is null)
        {
            return false;
        }

        db.JobServiceLines.Remove(line);
        await db.SaveChangesAsync(cancellationToken);
        await jobService.RecalculateTotalsAsync(line.JobId, cancellationToken);

        return true;
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
