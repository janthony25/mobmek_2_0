namespace MobmekApi.Services;

/// <summary>A composed invoice email, ready to send (or edit further) — subject/body are fixed
/// wording in v1 (no template system yet). Default recipient fields come from the customer on
/// file and may be null (e.g. no email on file), which callers must handle explicitly.</summary>
public record InvoiceEmailDraft(Guid? CustomerId, string? DefaultToAddress, string? DefaultToName, string Subject, string BodyHtml);

/// <summary>Builds the email subject/body for a generated invoice or quotation. A pure function
/// of its inputs (invoice + business letterhead), so it's easy to unit test independently of
/// sending.</summary>
public interface IEmailComposeService
{
    /// <summary>Null when the job/invoice combination doesn't exist.</summary>
    Task<InvoiceEmailDraft?> ComposeInvoiceEmailAsync(
        Guid jobId, Guid invoiceId, string? customIntro, CancellationToken cancellationToken = default);
}
