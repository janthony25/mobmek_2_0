using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class LabourService(AppDbContext db, IJobService jobService) : ILabourService
{
    public async Task<IReadOnlyList<LabourDto>> GetAllAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return await db.Labour.AsNoTracking()
            .Where(l => l.JobId == jobId)
            .OrderBy(l => l.CreatedAtUtc)
            .Select(l => ToDto(l))
            .ToListAsync(cancellationToken);
    }

    public async Task<LabourDto?> GetByIdAsync(Guid jobId, Guid id, CancellationToken cancellationToken = default)
    {
        var labour = await db.Labour.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id && l.JobId == jobId, cancellationToken);
        return labour is null ? null : ToDto(labour);
    }

    public async Task<LabourDto?> CreateAsync(Guid jobId, CreateLabourRequest request, CancellationToken cancellationToken = default)
    {
        if (!await db.Jobs.AnyAsync(j => j.Id == jobId, cancellationToken))
        {
            return null;
        }

        var labour = new Labour
        {
            JobId = jobId,
            Hours = request.Hours,
            RatePerHour = request.RatePerHour,
            FixedAmount = request.FixedAmount,
        };
        labour.TotalAmount = ComputeTotal(labour);

        db.Labour.Add(labour);
        await db.SaveChangesAsync(cancellationToken);
        await jobService.RecalculateTotalsAsync(labour.JobId, cancellationToken);

        return ToDto(labour);
    }

    public async Task<LabourDto?> UpdateAsync(Guid jobId, Guid id, UpdateLabourRequest request, CancellationToken cancellationToken = default)
    {
        var labour = await db.Labour.FirstOrDefaultAsync(l => l.Id == id && l.JobId == jobId, cancellationToken);
        if (labour is null)
        {
            return null;
        }

        labour.Hours = request.Hours;
        labour.RatePerHour = request.RatePerHour;
        labour.FixedAmount = request.FixedAmount;
        labour.TotalAmount = ComputeTotal(labour);

        await db.SaveChangesAsync(cancellationToken);
        await jobService.RecalculateTotalsAsync(labour.JobId, cancellationToken);

        return ToDto(labour);
    }

    public async Task<bool> DeleteAsync(Guid jobId, Guid id, CancellationToken cancellationToken = default)
    {
        var labour = await db.Labour.FirstOrDefaultAsync(l => l.Id == id && l.JobId == jobId, cancellationToken);
        if (labour is null)
        {
            return false;
        }

        db.Labour.Remove(labour);
        await db.SaveChangesAsync(cancellationToken);
        await jobService.RecalculateTotalsAsync(labour.JobId, cancellationToken);

        return true;
    }

    // FixedAmount wins when supplied; otherwise hours × rate (missing parts treated as 0).
    private static decimal ComputeTotal(Labour labour)
    {
        var total = labour.FixedAmount ?? (labour.Hours ?? 0m) * (labour.RatePerHour ?? 0m);
        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    private static LabourDto ToDto(Labour l) =>
        new(l.Id, l.JobId, l.Hours, l.RatePerHour, l.FixedAmount, l.TotalAmount, l.CreatedAtUtc, l.UpdatedAtUtc);
}
