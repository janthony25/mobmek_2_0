using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class JobItemService(AppDbContext db, IJobService jobService) : IJobItemService
{
    public async Task<IReadOnlyList<JobItemDto>> GetAllAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return await db.JobItems.AsNoTracking()
            .Where(i => i.JobId == jobId)
            .OrderBy(i => i.CreatedAtUtc)
            .Select(i => ToDto(i))
            .ToListAsync(cancellationToken);
    }

    public async Task<JobItemDto?> GetByIdAsync(Guid jobId, Guid id, CancellationToken cancellationToken = default)
    {
        var item = await db.JobItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id && i.JobId == jobId, cancellationToken);
        return item is null ? null : ToDto(item);
    }

    public async Task<JobItemDto?> CreateAsync(Guid jobId, CreateJobItemRequest request, CancellationToken cancellationToken = default)
    {
        if (!await db.Jobs.AnyAsync(j => j.Id == jobId, cancellationToken))
        {
            return null;
        }

        var item = new JobItem
        {
            JobId = jobId,
            ItemName = request.ItemName,
            TradePrice = request.TradePrice,
            RetailPrice = request.RetailPrice,
            MarkupSolution = request.MarkupSolution,
            Markup = request.Markup,
            ItemQuantity = request.ItemQuantity,
        };
        Apply(item, request.SellingPrice);

        db.JobItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        await jobService.RecalculateTotalsAsync(item.JobId, cancellationToken);

        return ToDto(item);
    }

    public async Task<JobItemDto?> UpdateAsync(Guid jobId, Guid id, UpdateJobItemRequest request, CancellationToken cancellationToken = default)
    {
        var item = await db.JobItems.FirstOrDefaultAsync(i => i.Id == id && i.JobId == jobId, cancellationToken);
        if (item is null)
        {
            return null;
        }

        item.ItemName = request.ItemName;
        item.TradePrice = request.TradePrice;
        item.RetailPrice = request.RetailPrice;
        item.MarkupSolution = request.MarkupSolution;
        item.Markup = request.Markup;
        item.ItemQuantity = request.ItemQuantity;
        Apply(item, request.SellingPrice);

        await db.SaveChangesAsync(cancellationToken);
        await jobService.RecalculateTotalsAsync(item.JobId, cancellationToken);

        return ToDto(item);
    }

    public async Task<bool> DeleteAsync(Guid jobId, Guid id, CancellationToken cancellationToken = default)
    {
        var item = await db.JobItems.FirstOrDefaultAsync(i => i.Id == id && i.JobId == jobId, cancellationToken);
        if (item is null)
        {
            return false;
        }

        db.JobItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        await jobService.RecalculateTotalsAsync(item.JobId, cancellationToken);

        return true;
    }

    /// <summary>
    /// Computes SellingPrice, UnitProfit and ItemTotal. With a trade price, the selling price is
    /// the markup applied to it (% or $); without one, the manually supplied selling price is used.
    /// </summary>
    private static void Apply(JobItem item, decimal? manualSellingPrice)
    {
        decimal selling;
        if (item.TradePrice is { } trade)
        {
            selling = item.MarkupSolution == MarkupSolution.Percentage
                ? trade * (1 + item.Markup / 100m)
                : trade + item.Markup;
        }
        else
        {
            selling = manualSellingPrice ?? 0m;
        }

        item.SellingPrice = Round(selling);
        item.UnitProfit = Round(item.SellingPrice - (item.TradePrice ?? 0m));
        item.ItemTotal = Round(item.SellingPrice * item.ItemQuantity);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static JobItemDto ToDto(JobItem i) =>
        new(i.Id, i.JobId, i.ItemName, i.TradePrice, i.RetailPrice, i.MarkupSolution, i.Markup,
            i.ItemQuantity, i.SellingPrice, i.UnitProfit, i.ItemTotal, i.CreatedAtUtc, i.UpdatedAtUtc);
}
