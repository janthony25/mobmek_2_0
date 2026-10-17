namespace MobmekApi.Entities;

/// <summary>
/// Durable outbox row: one pending push to Xero (docs/xero-integration-design.md §4), same shape
/// as <see cref="CalendarSyncItem"/>. Rows are deleted on success. No FK to <see cref="Invoice"/>
/// — invoices are never hard-deleted per <see cref="Invoice"/>'s own doc comment, but the pattern
/// is kept consistent with the calendar sync outbox regardless. At most one pending
/// <see cref="XeroSyncAction.PushInvoice"/> row per <see cref="InvoiceId"/> (unique partial index
/// in <see cref="Data.AppDbContext"/>) — in practice this never collides since an invoice is only
/// ever generated once, but the guard costs nothing.
/// </summary>
public class XeroSyncItem : BaseEntity
{
    public XeroSyncAction Action { get; set; }

    public Guid InvoiceId { get; set; }

    public int Attempts { get; set; }

    /// <summary>Due when &lt;= now. Backoff on failure: 1 min → 5 min → 30 min → hourly.</summary>
    public DateTime NextAttemptUtc { get; set; } = DateTime.UtcNow;

    public string? LastError { get; set; }
}
