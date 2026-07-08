using MobmekApi.Entities;

namespace MobmekApi.Services;

/// <summary>A file attached to an outbound message (e.g. the generated invoice/quotation PDF).</summary>
public record EmailAttachment(string FileName, string ContentType, byte[] Content);

/// <summary>One outbound message, provider-agnostic — everything <see cref="IEmailSender"/> needs to send it.</summary>
public record OutboundEmailMessage(
    string To, string? ToName, string? Cc, string? Bcc, string? ReplyTo, string FromName, string FromAddress,
    string Subject, string Html, IReadOnlyList<EmailAttachment>? Attachments = null);

/// <summary>Outcome of a send attempt. <c>NotConfigured</c> short-circuits before any network call.</summary>
public record EmailSendResult(bool Success, string? ProviderMessageId, string? ErrorMessage, bool NotConfigured = false);

/// <summary>Current provider-side status for a previously sent message, used by the poll job.</summary>
public record EmailProviderStatus(OutboundEmailStatus Status, string? Reason);

/// <summary>
/// Sends email through whatever provider is configured and reports back its delivery status.
/// Swapping providers (Resend → Postmark etc) is a new implementation of this interface, not a rewrite.
/// </summary>
public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(OutboundEmailMessage message, CancellationToken cancellationToken = default);

    Task<EmailProviderStatus> GetStatusAsync(string providerMessageId, CancellationToken cancellationToken = default);
}
