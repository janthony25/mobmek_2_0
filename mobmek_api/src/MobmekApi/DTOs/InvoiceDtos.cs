using System.ComponentModel.DataAnnotations;
using MobmekApi.Entities;

namespace MobmekApi.DTOs;

/// <summary>A single line on an invoice.</summary>
public record InvoiceItemDto(
    Guid Id,
    Guid InvoiceId,
    string ItemName,
    int Quantity,
    decimal ItemPrice,
    decimal ItemTotal);

/// <summary>
/// An invoice generated from a job. Every money field is a snapshot taken at generation time;
/// editing the job afterwards does not change it.
/// </summary>
public record InvoiceDto(
    Guid Id,
    Guid JobId,
    string InvoiceNumber,
    string IssueName,
    string? Notes,
    string DocumentType,
    string Status,
    DateOnly? DueDate,
    string? PaymentTerm,
    string? ModeOfPayment,
    decimal LabourPrice,
    decimal SubTotal,
    decimal GstRate,
    decimal TaxAmount,
    decimal Discount,
    decimal ShippingFee,
    decimal TotalAmount,
    bool IsPaid,
    decimal? AmountPaid,
    DateOnly? DatePaid,
    decimal? CashAmount,
    decimal? CardAmount,
    /// <summary>TotalAmount minus the sum of <see cref="PartialPayments"/>' amounts — never stored,
    /// always computed fresh. Equals TotalAmount when there are no partial payments.</summary>
    decimal RemainingBalance,
    IReadOnlyList<InvoicePartialPaymentDto> PartialPayments,
    IReadOnlyList<InvoiceItemDto> Items,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string? CustomerName,
    string? CustomerEmail,
    string? CarDescription,
    /// <summary>Status of the most recent email sent for this invoice, or null if never emailed.
    /// Only populated on the list/detail reads (<c>GetAllAsync</c>/<c>GetByIdAsync</c>) — a
    /// freshly generated/accepted/rejected/paid invoice returns null here even if it does have
    /// history, since the next list reload picks it up.</summary>
    OutboundEmailStatus? LatestEmailStatus,
    DateTime? LatestEmailedAtUtc);

/// <summary>One partial payment recorded against an invoice. See <see cref="Entities.InvoicePartialPayment"/>.</summary>
public record InvoicePartialPaymentDto(
    Guid Id,
    Guid InvoiceId,
    decimal Amount,
    string? ModeOfPayment,
    string? Notes,
    DateOnly PaymentDate,
    /// <summary>Opt-in — this payment only prints on the invoice PDF once toggled on. Defaults false.</summary>
    bool ShowOnInvoice,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>
/// Payload for recording a partial payment. <see cref="Amount"/> must be greater than zero and
/// no more than the invoice's current remaining balance. <see cref="PaymentDate"/> defaults to
/// today when omitted, but can be backdated to log a payment taken earlier. Always starts with
/// <see cref="Entities.InvoicePartialPayment.ShowOnInvoice"/> false — toggled separately.
/// </summary>
public record CreateInvoicePartialPaymentRequest(
    decimal Amount,
    [MaxLength(100)] string? ModeOfPayment,
    [MaxLength(4000)] string? Notes,
    DateOnly? PaymentDate);

/// <summary>
/// Payload for editing a recorded partial payment's note text. Only <see cref="Notes"/> can be
/// changed here — amount, date, and mode of payment are locked once the payment is recorded.
/// </summary>
public record UpdateInvoicePartialPaymentRequest([MaxLength(4000)] string? Notes);

/// <summary>Payload for toggling whether a recorded partial payment prints on the invoice PDF.</summary>
public record SetPartialPaymentVisibilityRequest(bool ShowOnInvoice);

/// <summary>
/// One row of the global Invoices/Quotations list — an invoice plus the job/customer/vehicle
/// context needed to browse and search across all jobs, without the line items.
/// </summary>
public record InvoiceListItemDto(
    Guid Id,
    Guid JobId,
    string InvoiceNumber,
    string IssueName,
    string DocumentType,
    string Status,
    string? CustomerName,
    string? CarDescription,
    DateOnly? DueDate,
    decimal TotalAmount,
    bool IsPaid,
    /// <summary>TotalAmount minus any partial payments recorded so far; equals TotalAmount when there are none.</summary>
    decimal RemainingBalance,
    DateTime CreatedAtUtc);

/// <summary>
/// Payload for generating an invoice from a job. The lines and totals are built automatically
/// from the job's items, labour and service lines — only this optional detail is supplied.
/// </summary>
public record CreateInvoiceRequest(DateOnly? DueDate);

/// <summary>
/// Payload for accepting a quotation. The new invoice copies the quotation's lines and totals;
/// only its due date is supplied here.
/// </summary>
public record AcceptQuotationRequest(DateOnly? DueDate);

/// <summary>
/// Payload for marking an invoice paid. <see cref="ModeOfPayment"/> and <see cref="PaymentTerm"/>
/// are captured here (not at generation) since they're only known once the customer actually pays.
/// The cash and card amounts are recorded as-supplied for payment-method analytics;
/// <see cref="DatePaid"/> defaults to today when omitted.
/// </summary>
public record MarkInvoicePaidRequest(
    [MaxLength(100)] string? ModeOfPayment,
    [MaxLength(100)] string? PaymentTerm,
    decimal? CashAmount,
    decimal? CardAmount,
    DateOnly? DatePaid);

/// <summary>
/// Failure reasons for invoice actions that have more than one way to fail — partial payment
/// create/edit/delete and reject — so the controller can return a specific status/message
/// instead of a generic 404. Same shape as <see cref="EmailWriteError"/>.
/// </summary>
public enum InvoiceActionError
{
    None,
    NotFound,
    /// <summary>The invoice is rejected or a quotation — never payable.</summary>
    NotPayable,
    /// <summary>The invoice is already fully paid — nothing left to partially pay.</summary>
    AlreadyPaid,
    /// <summary>Amount is not greater than zero, or exceeds the invoice's remaining balance.</summary>
    InvalidAmount,
    /// <summary>The partial payment id does not exist on this invoice.</summary>
    PaymentNotFound,
    /// <summary>Reject is blocked while the invoice still has partial payments recorded against it.</summary>
    HasPartialPayments,
}
