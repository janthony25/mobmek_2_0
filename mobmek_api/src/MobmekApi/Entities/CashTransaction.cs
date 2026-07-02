namespace MobmekApi.Entities;

/// <summary>
/// One actual cash movement in or out of a <see cref="CashAccount"/> — the ledger these rows
/// form is the single source of truth for cash. <see cref="Amount"/> is always positive
/// (<see cref="Direction"/> carries the sign) and GST-inclusive.
///
/// Rows auto-posted from an invoice payment carry <see cref="InvoiceId"/> and are corrected
/// from the invoice, never edited or deleted directly. A transfer between accounts is two
/// paired legs sharing a <see cref="TransferGroupId"/>; transfer legs move account balances
/// but are excluded from inflow/outflow reporting totals.
/// </summary>
public class CashTransaction : BaseEntity
{
    public Guid AccountId { get; set; }

    public CashAccount? Account { get; set; }

    /// <summary>"In" or "Out".</summary>
    public required string Direction { get; set; }

    /// <summary>Always positive; GST-inclusive.</summary>
    public decimal Amount { get; set; }

    /// <summary>The date the cash actually moved (what payments-basis GST works from).</summary>
    public DateOnly Date { get; set; }

    public required string Description { get; set; }

    public Guid CategoryId { get; set; }

    public TransactionCategory? Category { get; set; }

    /// <summary>Supplier / payer name (display text; copied from the payee when one is linked).</summary>
    public string? Counterparty { get; set; }

    /// <summary>Optional link to a normalized <see cref="Payee"/>; kept (not cascaded) if the payee is deleted.</summary>
    public Guid? PayeeId { get; set; }

    public Payee? Payee { get; set; }

    /// <summary>
    /// "Pending" (entered but not yet confirmed against the bank), "Cleared" (the default for
    /// manual entries — the money moved) or "Reconciled" (ticked off in a completed
    /// reconciliation; immutable from then on).
    /// </summary>
    public string Status { get; set; } = "Cleared";

    /// <summary>Set when this row was auto-posted from an invoice payment; cleared (not cascaded) if the invoice's job is deleted, because the money still moved.</summary>
    public Guid? InvoiceId { get; set; }

    public Invoice? Invoice { get; set; }

    /// <summary>Pairs the two legs of an account-to-account transfer; both legs are managed together.</summary>
    public Guid? TransferGroupId { get; set; }

    /// <summary>
    /// Groups sibling rows entered as one split payment (one real-world payment covering
    /// several categories). Split rows are edited/deleted as a group, like transfer legs.
    /// </summary>
    public Guid? SplitGroupId { get; set; }

    /// <summary>Set when this row was materialised from a <see cref="RecurringTransaction"/> occurrence; kept (not cascaded) if the schedule is deleted, because the money still moved.</summary>
    public Guid? RecurringTransactionId { get; set; }

    public RecurringTransaction? RecurringTransaction { get; set; }

    /// <summary>"Taxable", "Exempt" or "ZeroRated" — drives the GST estimate (GST content of a taxable amount = Amount × rate ÷ (1 + rate)).</summary>
    public string GstTreatment { get; set; } = "Taxable";

    public string? Notes { get; set; }

    // Receipts/documents (cascade-deleted with the transaction).
    public ICollection<TransactionAttachment> Attachments { get; set; } = [];
}
