using MobmekApi.Entities;
using MobmekApi.LegacyImport.Legacy;

namespace MobmekApi.LegacyImport.Mappers;

/// <summary>
/// Legacy JobItem → new JobItem (design §3.4). All stored computed money fields
/// (SellingPrice, UnitProfit, ItemTotal) are copied verbatim — never recomputed.
/// </summary>
public static class JobItemMapper
{
    public static JobItem Map(LegacyJobItem legacy, Guid jobId) => new()
    {
        JobId = jobId,
        ItemName = legacy.ItemName.Trim(),
        TradePrice = legacy.TradePrice,
        RetailPrice = legacy.RetailPrice,
        // Real data contains only "%" (518) and "$" (8).
        MarkupSolution = legacy.MarkupSolution.Trim() == "$" ? MarkupSolution.Dollar : MarkupSolution.Percentage,
        Markup = legacy.Markup,
        ItemQuantity = legacy.ItemQuantity,
        SellingPrice = legacy.SellingPrice,
        UnitProfit = legacy.UnitProfit,
        ItemTotal = legacy.ItemTotal,
        CreatedAtUtc = NzTime.ToUtc(legacy.DateAdded),
        UpdatedAtUtc = NzTime.ToUtc(legacy.DateEdited),
    };
}

/// <summary>
/// Legacy Labour → new Labour (design §3.4): hours × rate model, totals copied verbatim.
/// The legacy LabourName has no home on the new entity — JobMapper routes it into JobNotes.
/// </summary>
public static class LabourMapper
{
    public static Labour Map(LegacyLabour legacy, Guid jobId) => new()
    {
        JobId = jobId,
        Hours = legacy.LabourHours,
        RatePerHour = legacy.LabourPrice,
        FixedAmount = null,
        TotalAmount = legacy.TotalLabour,
        CreatedAtUtc = NzTime.ToUtc(legacy.DateAdded),
        UpdatedAtUtc = NzTime.ToUtc(legacy.DateEdited),
    };
}

/// <summary>
/// Legacy JobService join → new JobServiceLine (design §3.4). The old model charged the
/// catalog price plus a per-job AdditionalAmount, so the snapshot is their sum; Quantity 1.
/// </summary>
public static class ServiceLineMapper
{
    public static JobServiceLine Map(LegacyJobServiceJoin legacy, Guid jobId, Guid jobServiceId, decimal catalogPrice)
    {
        var unitPrice = catalogPrice + legacy.AdditionalAmount;
        return new JobServiceLine
        {
            JobId = jobId,
            JobServiceId = jobServiceId,
            UnitPrice = unitPrice,
            Quantity = 1,
            LineTotal = unitPrice,
            CreatedAtUtc = NzTime.ToUtc(legacy.DateAdded),
        };
    }
}
