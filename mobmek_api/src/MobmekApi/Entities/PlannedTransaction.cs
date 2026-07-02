namespace MobmekApi.Entities;

/// <summary>
/// A known future one-off cash movement (equipment purchase, expected grant, tax payment
/// override) that isn't already covered by a recurring schedule or an unpaid invoice.
/// Expected income from customers is not duplicated here — unpaid, non-rejected invoices
/// already are the receivables book the forecast reads directly.
/// </summary>
public class PlannedTransaction : BaseEntity
{
    public required string Description { get; set; }

    /// <summary>"In" or "Out".</summary>
    public required string Direction { get; set; }

    /// <summary>Always positive; GST-inclusive, same convention as <see cref="CashTransaction.Amount"/>.</summary>
    public decimal Amount { get; set; }

    public DateOnly ExpectedDate { get; set; }

    public Guid CategoryId { get; set; }

    public TransactionCategory? Category { get; set; }

    /// <summary>Optional — a planned item doesn't have to commit to an account ahead of time.</summary>
    public Guid? AccountId { get; set; }

    public CashAccount? Account { get; set; }

    /// <summary>"Planned" (editable) → "Posted" or "Cancelled" (terminal).</summary>
    public string Status { get; set; } = "Planned";

    /// <summary>Null = included in every scenario ("Always"); "BestCase" or "WorstCase" confines it to a what-if scenario.</summary>
    public string? ScenarioTag { get; set; }
}
