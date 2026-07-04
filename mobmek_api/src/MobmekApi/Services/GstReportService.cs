using MobmekApi.Data;
using MobmekApi.DTOs;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class GstReportService(AppDbContext db, IGstSettingService gstSettingService) : IGstReportService
{
    public async Task<GstReportDto> GetReportAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
    {
        var rate = (await gstSettingService.GetCurrentAsync(cancellationToken)).Rate;

        // Transfer legs move money between accounts rather than representing a sale/purchase,
        // so they're excluded here the same way they're excluded from inflow/outflow totals elsewhere.
        var rows = await db.CashTransactions.AsNoTracking()
            .Include(t => t.Account)
            .Where(t => t.GstTreatment == "Taxable" && t.TransferGroupId == null)
            .Where(t => t.Date >= start && t.Date <= end)
            .Select(t => new { t.Direction, t.Amount, AccountType = t.Account!.Type })
            .ToListAsync(cancellationToken);

        var included = Summarize(rows.Select(r => (r.Direction, r.Amount)), rate);
        var excluded = Summarize(rows.Where(r => r.AccountType != "Cash").Select(r => (r.Direction, r.Amount)), rate);
        var cashGst = included.NetGst - excluded.NetGst;

        return new GstReportDto(start, end, included, excluded, cashGst);
    }

    private static GstScopeTotalsDto Summarize(IEnumerable<(string Direction, decimal Amount)> rows, decimal rate)
    {
        decimal gstOnSales = 0m, gstOnPurchases = 0m;
        foreach (var (direction, amount) in rows)
        {
            var gstContent = amount * rate / (1 + rate);
            if (direction == "In") gstOnSales += gstContent;
            else gstOnPurchases += gstContent;
        }

        return new GstScopeTotalsDto(gstOnSales, gstOnPurchases, gstOnSales - gstOnPurchases);
    }
}
