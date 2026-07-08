using MobmekApi.DTOs;
using MobmekApi.Entities;

namespace MobmekApi.Services;

/// <summary>The minimum a status-poll needs — never the full entity, per the "entities never
/// leave the service layer" convention.</summary>
public record PendingEmailCheck(Guid Id, string ProviderMessageId);

/// <summary>
/// The send pipeline: compose → write a <see cref="OutboundEmailStatus.Queued"/> row → call the
/// provider → update to Sent/Failed. Every send is audited before the provider is ever called.
/// Retrying creates a new row; existing rows are never overwritten except through the
/// no-regress status state machine (<see cref="ApplyStatusAsync"/>).
/// </summary>
public interface IOutboundEmailService
{
    Task<(OutboundEmailDto? Email, EmailWriteError Error)> SendInvoiceEmailAsync(
        Guid jobId, Guid invoiceId, SendInvoiceEmailRequest request, CancellationToken cancellationToken = default);

    Task<(OutboundEmailDto? Email, EmailWriteError Error)> SendTestEmailAsync(
        string toAddress, CancellationToken cancellationToken = default);

    Task<OutboundEmailPageDto> GetPagedAsync(OutboundEmailFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Only allowed from Failed/Bounced; always creates a new row.</summary>
    Task<(OutboundEmailDto? Email, EmailWriteError Error)> RetryAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The stored rendered HTML for display, or null when the row doesn't exist.</summary>
    Task<string?> GetPreviewHtmlAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Rows the status-poll job should check: Sent and younger than 72 hours.</summary>
    Task<IReadOnlyList<PendingEmailCheck>> GetPendingStatusChecksAsync(CancellationToken cancellationToken = default);

    /// <summary>Applies a provider status update if — and only if — it's a forward move in the
    /// state machine (Queued &lt; Sent &lt; terminal). A late/duplicate event that would regress
    /// an already-terminal row is silently ignored.</summary>
    Task ApplyStatusAsync(Guid id, OutboundEmailStatus newStatus, string? reason, DateTime eventAtUtc, CancellationToken cancellationToken = default);
}
