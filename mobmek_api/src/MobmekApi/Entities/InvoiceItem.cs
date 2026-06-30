namespace MobmekApi.Entities;

/// <summary>
/// A single line on an <see cref="Invoice"/>, snapshotted from the job's items, labour and
/// service lines when the invoice was generated. Plain values — no recomputation.
/// </summary>
public class InvoiceItem : BaseEntity
{
    public Guid InvoiceId { get; set; }

    public Invoice? Invoice { get; set; }

    public required string ItemName { get; set; }

    public int Quantity { get; set; }

    public decimal ItemPrice { get; set; }

    public decimal ItemTotal { get; set; }
}
