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

    public string? Notes { get; set; }

    /// <summary>"Invoice" for now (quotations may be added later).</summary>
    public string DocumentType { get; set; } = "Invoice";

    /// <summary>"Active" or "Rejected".</summary>
    public string Status { get; set; } = "Active";

    public DateOnly? DueDate { get; set; }

    public string? PaymentTerm { get; set; }

    public string? ModeOfPayment { get; set; }

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

    // Lines (cascade-deleted with the invoice).
    public ICollection<InvoiceItem> Items { get; set; } = [];
}
