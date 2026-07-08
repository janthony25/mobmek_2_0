using System.ComponentModel.DataAnnotations;
using MobmekApi.Entities;

namespace MobmekApi.DTOs;

/// <summary>One send-attempt row. <c>BodyHtml</c> is deliberately excluded — see the
/// separate <c>preview</c> endpoint, which returns it as raw HTML rather than JSON.</summary>
public record OutboundEmailDto(
    Guid Id,
    string ToAddress,
    string? ToName,
    string? CcAddresses,
    string Subject,
    OutboundEmailStatus Status,
    string? ErrorMessage,
    OutboundEmailKind Kind,
    Guid? CustomerId,
    Guid? InvoiceId,
    DateTime? SentAtUtc,
    DateTime? DeliveredAtUtc,
    DateTime? FailedAtUtc,
    DateTime CreatedAtUtc);

public record OutboundEmailPageDto(
    IReadOnlyList<OutboundEmailDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

/// <summary>Filter for the send-history list; all criteria optional.</summary>
public record OutboundEmailFilter(
    Guid? CustomerId,
    Guid? InvoiceId,
    OutboundEmailStatus? Status,
    OutboundEmailKind? Kind,
    int Page = 1,
    int PageSize = 50);

/// <summary>Payload for emailing an invoice. <c>Intro</c> is a free-text paragraph shown above
/// the generated invoice block; <c>Subject</c>/<c>Intro</c> are pre-filled by the compose modal
/// from fixed wording but editable per send.</summary>
public record SendInvoiceEmailRequest(
    [Required, EmailAddress] string To,
    string? ToName,
    [EmailAddress] string? Cc,
    [Required, MaxLength(500)] string Subject,
    string? Intro);

/// <summary>Outcome of a send attempt that depends on state outside the request body
/// (missing invoice, unconfigured provider, non-retryable row).</summary>
public enum EmailWriteError
{
    None,
    InvoiceNotFound,
    MissingRecipient,
    NotConfigured,
    NotFound,
    NotRetryable,
}
