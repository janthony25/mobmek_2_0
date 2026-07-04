namespace MobmekApi.DTOs;

/// <summary>
/// GST on sales/purchases (and the net of the two) for one scope of transactions
/// over a date range. Only <c>Taxable</c> transactions contribute; GST content is
/// estimated as <c>Amount × rate ÷ (1 + rate)</c>.
/// </summary>
public record GstScopeTotalsDto(decimal GstOnSales, decimal GstOnPurchases, decimal NetGst);

/// <summary>
/// Side-by-side GST view for review purposes only — it does not affect what's actually
/// filed or remitted. <see cref="Included"/> covers every account; <see cref="Excluded"/>
/// covers only non-Cash accounts (bank/card/digital wallet), so <see cref="CashGst"/>
/// (their difference) shows how much of the net GST sits in cash that could go missing
/// before it's banked.
/// </summary>
public record GstReportDto(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    GstScopeTotalsDto Included,
    GstScopeTotalsDto Excluded,
    decimal CashGst);
