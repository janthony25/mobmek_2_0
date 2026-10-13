using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface IInvoiceService
{
    /// <summary>Lists the invoices generated for a job, newest first.</summary>
    Task<IReadOnlyList<InvoiceDto>> GetAllAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one page of invoices or quotations across all jobs, filtered by
    /// <paramref name="documentType"/> ("Invoice" or "Quotation"). <paramref name="search"/>
    /// matches the customer name or car rego, case-insensitively. <paramref name="sortBy"/> is
    /// "newest" (default), "oldest", "amountDesc", or "amountAsc". <paramref name="status"/>
    /// filters to "Active", "Accepted" (quotations only), or "Rejected". <paramref name="isPaid"/>
    /// filters invoices by payment state. <paramref name="dateFrom"/>/<paramref name="dateTo"/>
    /// filter by the issue date (inclusive).
    /// </summary>
    Task<PagedResult<InvoiceListItemDto>> GetPagedAsync(
        string documentType, int page, int pageSize, string? search,
        string? sortBy = null, string? status = null, bool? isPaid = null,
        DateOnly? dateFrom = null, DateOnly? dateTo = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns one invoice, only if it belongs to <paramref name="jobId"/>.</summary>
    Task<InvoiceDto?> GetByIdAsync(Guid jobId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a new invoice from the job's items, labour and service lines, snapshotting the
    /// lines, totals and current GST rate. Returns <c>null</c> when the job does not exist.
    /// </summary>
    Task<InvoiceDto?> GenerateAsync(Guid jobId, CreateInvoiceRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a quotation from the job's items, labour and service lines. Priced exactly like
    /// an invoice (same line snapshotting and GST treatment) but carries DocumentType
    /// "Quotation", numbers independently (QUO-0001, ...), is valid for exactly 30 days after
    /// issue (DueDate is set to issue date + 30 days; the request's due date is ignored), and
    /// can never be marked paid. Returns <c>null</c> when the job does not exist.
    /// </summary>
    Task<InvoiceDto?> GenerateQuotationAsync(Guid jobId, CreateInvoiceRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts a quotation: marks it "Accepted" and generates a new invoice (next INV number)
    /// copying the quotation's snapshotted lines, totals and GST rate — the customer pays
    /// exactly what they accepted, even if the job's lines changed after the quotation was
    /// issued. Returns the new invoice, or <c>null</c> when there is no "Active" quotation
    /// with that id on <paramref name="jobId"/> (not found, not a quotation, or already
    /// accepted/rejected).
    /// </summary>
    Task<InvoiceDto?> AcceptQuotationAsync(Guid jobId, Guid id, AcceptQuotationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an invoice as rejected (kept for the record, never deleted) and removes any
    /// cash-flow ledger rows its payment posted. Fails with <see cref="InvoiceActionError.NotFound"/>
    /// when the invoice is not found on <paramref name="jobId"/>, or
    /// <see cref="InvoiceActionError.HasPartialPayments"/> when it still has partial payments
    /// recorded — those must be removed first so a human explicitly decides what happens to
    /// that money rather than it being silently reversed.
    /// </summary>
    Task<(InvoiceDto? Invoice, InvoiceActionError Error)> RejectAsync(Guid jobId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an invoice as paid, snapshotting the payment date and the cash/card split, and
    /// posts the payment into the cash-flow ledger (routed by the CashFlowSettings account
    /// mapping; skipped while no routing is configured). Returns <c>null</c> when there is no
    /// payable ("Active") invoice with that id on <paramref name="jobId"/> (not found,
    /// already rejected, or a quotation — quotations are never payable).
    /// </summary>
    Task<InvoiceDto?> MarkPaidAsync(Guid jobId, Guid id, MarkInvoicePaidRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a partial payment against an invoice — a record-keeping entry only (amount,
    /// mode, date, notes), not a cash-flow ledger posting (see feature-gaps.md). Never touches
    /// <see cref="Invoice.Notes"/> or the job's JobNotes/InvoiceNotes — those print on the
    /// customer-facing PDF/job form and stay purely free-text; the payment history is surfaced
    /// only via <see cref="InvoiceDto.PartialPayments"/>. Fails with
    /// <see cref="InvoiceActionError.NotFound"/>, <see cref="InvoiceActionError.NotPayable"/>
    /// (rejected/quotation), <see cref="InvoiceActionError.AlreadyPaid"/>, or
    /// <see cref="InvoiceActionError.InvalidAmount"/> (not greater than zero, or more than the
    /// current remaining balance).
    /// </summary>
    Task<(InvoiceDto? Invoice, InvoiceActionError Error)> AddPartialPaymentAsync(
        Guid jobId, Guid id, CreateInvoicePartialPaymentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Edits a recorded partial payment's note text — amount, date, and mode of payment are
    /// locked once recorded. Fails with <see cref="InvoiceActionError.NotFound"/> or
    /// <see cref="InvoiceActionError.PaymentNotFound"/>.
    /// </summary>
    Task<(InvoiceDto? Invoice, InvoiceActionError Error)> UpdatePartialPaymentNotesAsync(
        Guid jobId, Guid id, Guid paymentId, UpdateInvoicePartialPaymentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles whether a recorded partial payment prints on the invoice PDF (as a line after
    /// the Notes section) — defaults off when recorded, opt-in only. Fails with
    /// <see cref="InvoiceActionError.NotFound"/> or <see cref="InvoiceActionError.PaymentNotFound"/>.
    /// </summary>
    Task<(InvoiceDto? Invoice, InvoiceActionError Error)> SetPartialPaymentVisibilityAsync(
        Guid jobId, Guid id, Guid paymentId, SetPartialPaymentVisibilityRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a recorded partial payment. Fails with <see cref="InvoiceActionError.NotFound"/>
    /// or <see cref="InvoiceActionError.PaymentNotFound"/>.
    /// </summary>
    Task<(InvoiceDto? Invoice, InvoiceActionError Error)> DeletePartialPaymentAsync(
        Guid jobId, Guid id, Guid paymentId, CancellationToken cancellationToken = default);
}
