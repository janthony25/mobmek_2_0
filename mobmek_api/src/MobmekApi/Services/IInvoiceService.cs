using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface IInvoiceService
{
    /// <summary>Lists the invoices generated for a job, newest first.</summary>
    Task<IReadOnlyList<InvoiceDto>> GetAllAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Returns one invoice, only if it belongs to <paramref name="jobId"/>.</summary>
    Task<InvoiceDto?> GetByIdAsync(Guid jobId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a new invoice from the job's items, labour and service lines, snapshotting the
    /// lines, totals and current GST rate. Returns <c>null</c> when the job does not exist.
    /// </summary>
    Task<InvoiceDto?> GenerateAsync(Guid jobId, CreateInvoiceRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an invoice as rejected (kept for the record, never deleted) and removes any
    /// cash-flow ledger rows its payment posted. Returns <c>null</c> when the invoice is
    /// not found on <paramref name="jobId"/>.
    /// </summary>
    Task<InvoiceDto?> RejectAsync(Guid jobId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an invoice as paid, snapshotting the payment date and the cash/card split, and
    /// posts the payment into the cash-flow ledger (routed by the CashFlowSettings account
    /// mapping; skipped while no routing is configured). Returns <c>null</c> when there is no
    /// payable ("Active") invoice with that id on <paramref name="jobId"/> (not found or
    /// already rejected).
    /// </summary>
    Task<InvoiceDto?> MarkPaidAsync(Guid jobId, Guid id, MarkInvoicePaidRequest request, CancellationToken cancellationToken = default);
}
