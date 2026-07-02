namespace MobmekApi.Entities;

/// <summary>
/// An invoice generated from a <see cref="Job"/>. The money fields and the GST rate are
/// snapshotted at generation time, so later edits to the job or the GST setting do not
/// change an existing invoice. Invoices are never deleted; a rejected one keeps
/// <see cref="Status"/> = "Rejected" so it stays trackable.
/// </summary>
public class Invoice : BaseEntity
{
    /// <summary>The job this invoice was generated from (foreign key).</summary>
    public Guid JobId { get; set; }

    public Job? Job { get; set; }

    /// <summary>Headline for the invoice, captured from the job's title.</summary>
    public required string IssueName { get; set; }

    /// <summary>
    /// Business-wide sequential number backing the printed document ID, counted per
    /// <see cref="DocumentType"/> (INV-0001 for invoices, QUO-0001 for quotations). Assigned
    /// in <see cref="Services.InvoiceService"/> as (current max + 1); not DB-generated, so it
    /// isn't safe against concurrent generation, matching the app's other invoice invariants
    /// (see todo-list.md) that aren't yet guarded for concurrency.
    /// </summary>
    public int SequenceNumber { get; set; }

    public string? Notes { get; set; }

    /// <summary>"Invoice" or "Quotation". A quotation is priced like an invoice but is never payable.</summary>
    public string DocumentType { get; set; } = "Invoice";

    /// <summary>"Active" or "Rejected".</summary>
    public string Status { get; set; } = "Active";

    public DateOnly? DueDate { get; set; }

    // --- Snapshotted money fields (computed at generation, never recalculated) ---

    public decimal LabourPrice { get; set; }

    public decimal SubTotal { get; set; }

    /// <summary>GST rate applied, as a fraction (snapshot of the GST setting at generation; 0.15 = 15%).</summary>
    public decimal GstRate { get; set; }

    /// <summary>GST portion of the subtotal. Tax is inclusive — already part of the prices, stored for display.</summary>
    public decimal TaxAmount { get; set; }

    public decimal Discount { get; set; }

    public decimal ShippingFee { get; set; }

    public decimal TotalAmount { get; set; }

    // --- Payment lifecycle (set when the invoice is marked paid) ---

    /// <summary>Whether the invoice has been settled.</summary>
    public bool IsPaid { get; set; }

    /// <summary>Amount received, snapshotted when marked paid (normally equals <see cref="TotalAmount"/>).</summary>
    public decimal? AmountPaid { get; set; }

    /// <summary>Date the invoice was marked paid.</summary>
    public DateOnly? DatePaid { get; set; }

    /// <summary>Payment term the customer was given, recorded when the invoice is marked paid.</summary>
    public string? PaymentTerm { get; set; }

    /// <summary>How the customer paid (e.g. Cash, Card, Bank Transfer), recorded when marked paid.</summary>
    public string? ModeOfPayment { get; set; }

    /// <summary>Portion of the payment taken in cash (for payment-method analytics).</summary>
    public decimal? CashAmount { get; set; }

    /// <summary>Portion of the payment taken by card (for payment-method analytics).</summary>
    public decimal? CardAmount { get; set; }

    // Lines (cascade-deleted with the invoice).
    public ICollection<InvoiceItem> Items { get; set; } = [];
}
