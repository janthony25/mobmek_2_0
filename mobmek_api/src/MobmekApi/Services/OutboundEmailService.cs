using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class OutboundEmailService(
    AppDbContext db,
    IEmailComposeService composeService,
    IInvoicePdfService pdfService,
    IEmailSender emailSender,
    IEmailSettingsService emailSettingsService) : IOutboundEmailService
{
    private const int MaxPageSize = 200;

    public async Task<(OutboundEmailDto? Email, EmailWriteError Error)> SendInvoiceEmailAsync(
        Guid jobId, Guid invoiceId, SendInvoiceEmailRequest request, CancellationToken cancellationToken = default)
    {
        var draft = await composeService.ComposeInvoiceEmailAsync(jobId, invoiceId, request.Intro, cancellationToken);
        if (draft is null)
        {
            return (null, EmailWriteError.InvoiceNotFound);
        }

        var attachment = await BuildPdfAttachmentAsync(jobId, invoiceId, cancellationToken);

        return await SendAsync(
            request.To, request.ToName, request.Cc, request.Subject, draft.BodyHtml,
            OutboundEmailKind.Invoice, draft.CustomerId, invoiceId, attachment, cancellationToken);
    }

    public Task<(OutboundEmailDto? Email, EmailWriteError Error)> SendTestEmailAsync(
        string toAddress, CancellationToken cancellationToken = default)
    {
        const string html = "<p>This is a test email from Mobmek — if you're reading this, outbound email is configured correctly.</p>";
        return SendAsync(toAddress, null, null, "Mobmek test email", html, OutboundEmailKind.Test, null, null, null, cancellationToken);
    }

    /// <summary>Renders the invoice/quotation PDF and wraps it as an attachment; null (no
    /// attachment) if the invoice can't be found — the caller has already validated existence
    /// via <see cref="IEmailComposeService"/>, so this only realistically happens if it was
    /// deleted in the moment between the two calls.</summary>
    private async Task<EmailAttachment?> BuildPdfAttachmentAsync(Guid jobId, Guid invoiceId, CancellationToken cancellationToken)
    {
        var pdf = await pdfService.GenerateAsync(jobId, invoiceId, cancellationToken);
        return pdf is null ? null : new EmailAttachment(pdf.FileName, "application/pdf", pdf.Bytes);
    }

    public async Task<OutboundEmailPageDto> GetPagedAsync(OutboundEmailFilter filter, CancellationToken cancellationToken = default)
    {
        var query = db.OutboundEmails.AsNoTracking().AsQueryable();

        if (filter.CustomerId is not null) query = query.Where(e => e.CustomerId == filter.CustomerId);
        if (filter.InvoiceId is not null) query = query.Where(e => e.InvoiceId == filter.InvoiceId);
        if (filter.Status is not null) query = query.Where(e => e.Status == filter.Status);
        if (filter.Kind is not null) query = query.Where(e => e.Kind == filter.Kind);

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Clamp(filter.PageSize, 1, MaxPageSize);

        var items = await query
            .OrderByDescending(e => e.CreatedAtUtc).ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new OutboundEmailPageDto(items.Select(ToDto).ToList(), page, pageSize, totalCount);
    }

    public async Task<(OutboundEmailDto? Email, EmailWriteError Error)> RetryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var original = await db.OutboundEmails.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (original is null)
        {
            return (null, EmailWriteError.NotFound);
        }

        if (original.Status is not (OutboundEmailStatus.Failed or OutboundEmailStatus.Bounced))
        {
            return (null, EmailWriteError.NotRetryable);
        }

        // The PDF isn't stored on the row (only BodyHtml is) — regenerate it from the invoice
        // for an invoice-linked send. The row has no JobId column, so look it up via the invoice.
        EmailAttachment? attachment = null;
        if (original.InvoiceId is { } invoiceId)
        {
            var jobId = await db.Invoices.AsNoTracking()
                .Where(i => i.Id == invoiceId)
                .Select(i => (Guid?)i.JobId)
                .FirstOrDefaultAsync(cancellationToken);
            if (jobId is not null)
            {
                attachment = await BuildPdfAttachmentAsync(jobId.Value, invoiceId, cancellationToken);
            }
        }

        return await SendAsync(
            original.ToAddress, original.ToName, original.CcAddresses, original.Subject, original.BodyHtml,
            original.Kind, original.CustomerId, original.InvoiceId, attachment, cancellationToken);
    }

    public Task<string?> GetPreviewHtmlAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.OutboundEmails.AsNoTracking().Where(e => e.Id == id).Select(e => e.BodyHtml).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PendingEmailCheck>> GetPendingStatusChecksAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddHours(-72);
        return await db.OutboundEmails.AsNoTracking()
            .Where(e => e.Status == OutboundEmailStatus.Sent && e.SentAtUtc != null && e.SentAtUtc >= cutoff && e.ProviderMessageId != null)
            .Select(e => new PendingEmailCheck(e.Id, e.ProviderMessageId!))
            .ToListAsync(cancellationToken);
    }

    public async Task ApplyStatusAsync(Guid id, OutboundEmailStatus newStatus, string? reason, DateTime eventAtUtc, CancellationToken cancellationToken = default)
    {
        var email = await db.OutboundEmails.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (email is null || Rank(newStatus) <= Rank(email.Status))
        {
            return;
        }

        email.Status = newStatus;
        email.ErrorMessage = reason;
        if (newStatus == OutboundEmailStatus.Delivered)
        {
            email.DeliveredAtUtc = eventAtUtc;
        }
        else if (newStatus == OutboundEmailStatus.Failed)
        {
            email.FailedAtUtc = eventAtUtc;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    // Queued(0) -> Sent(1) -> a terminal outcome(2). Once at rank 2, nothing can move it again —
    // this is what stops a late "Delivered" event from overwriting an already-"Bounced" row.
    private static int Rank(OutboundEmailStatus status) => status switch
    {
        OutboundEmailStatus.Queued => 0,
        OutboundEmailStatus.Sent => 1,
        _ => 2,
    };

    private async Task<(OutboundEmailDto? Email, EmailWriteError Error)> SendAsync(
        string to, string? toName, string? cc, string subject, string bodyHtml,
        OutboundEmailKind kind, Guid? customerId, Guid? invoiceId, EmailAttachment? attachment, CancellationToken cancellationToken)
    {
        var settings = await emailSettingsService.GetCurrentAsync(cancellationToken);
        if (!settings.ResendConfigured)
        {
            // Nothing to audit — the send never reached "queued", so no row is written.
            return (null, EmailWriteError.NotConfigured);
        }

        var email = new OutboundEmail
        {
            ToAddress = to,
            ToName = toName,
            CcAddresses = string.IsNullOrWhiteSpace(cc) ? null : cc,
            Subject = subject,
            BodyHtml = bodyHtml,
            Status = OutboundEmailStatus.Queued,
            Kind = kind,
            CustomerId = customerId,
            InvoiceId = invoiceId,
        };
        db.OutboundEmails.Add(email);
        await db.SaveChangesAsync(cancellationToken);

        var message = new OutboundEmailMessage(
            To: to, ToName: toName, Cc: cc,
            Bcc: settings.BccSelf ? settings.ReplyToAddress : null,
            ReplyTo: settings.ReplyToAddress,
            FromName: settings.FromName, FromAddress: settings.FromAddress,
            Subject: subject, Html: bodyHtml,
            Attachments: attachment is null ? null : [attachment]);

        var result = await emailSender.SendAsync(message, cancellationToken);

        if (result.Success)
        {
            email.Status = OutboundEmailStatus.Sent;
            email.SentAtUtc = DateTime.UtcNow;
            email.ProviderMessageId = result.ProviderMessageId;
        }
        else
        {
            // Covers the ordinary rejection case and the rare race where configuration
            // disappeared between the check above and this call — either way the row we
            // already committed needs a terminal outcome, not to be left dangling as Queued.
            email.Status = OutboundEmailStatus.Failed;
            email.FailedAtUtc = DateTime.UtcNow;
            email.ErrorMessage = result.ErrorMessage ?? "Email provider is not configured.";
        }

        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(email), EmailWriteError.None);
    }

    private static OutboundEmailDto ToDto(OutboundEmail e) => new(
        e.Id, e.ToAddress, e.ToName, e.CcAddresses, e.Subject, e.Status, e.ErrorMessage, e.Kind,
        e.CustomerId, e.InvoiceId, e.SentAtUtc, e.DeliveredAtUtc, e.FailedAtUtc, e.CreatedAtUtc);
}
