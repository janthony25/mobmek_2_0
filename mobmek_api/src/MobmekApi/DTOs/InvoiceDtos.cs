using System.ComponentModel.DataAnnotations;

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
    IReadOnlyList<InvoiceItemDto> Items,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

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
