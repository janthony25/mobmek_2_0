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
    IReadOnlyList<InvoiceItemDto> Items,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>
/// Payload for generating an invoice from a job. The lines and totals are built automatically
/// from the job's items, labour and service lines — only these optional details are supplied.
/// </summary>
public record CreateInvoiceRequest(
    DateOnly? DueDate,
    [MaxLength(100)] string? ModeOfPayment,
    [MaxLength(100)] string? PaymentTerm);
