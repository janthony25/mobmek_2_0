namespace MobmekApi.Services;

/// <summary>A generated invoice/quotation PDF, ready to attach to an email or stream to a caller.</summary>
public record InvoicePdfDocument(byte[] Bytes, string FileName);

/// <summary>Renders an invoice or quotation as a PDF (via QuestPDF) from the same data
/// <see cref="IEmailComposeService"/> uses — a pure function of the invoice + letterhead, so
/// it can be regenerated on demand rather than stored.</summary>
public interface IInvoicePdfService
{
    /// <summary>Null when the job/invoice combination doesn't exist.</summary>
    Task<InvoicePdfDocument?> GenerateAsync(Guid jobId, Guid invoiceId, CancellationToken cancellationToken = default);
}
