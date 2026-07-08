using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using MobmekApi.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using JobService = MobmekApi.Services.JobService;

namespace MobmekApi.Tests.Services;

public class OutboundEmailServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static IFileStorage CreateStorage() =>
        new LocalFileStorage(Path.Combine(Path.GetTempPath(), "mobmek-tests", Guid.NewGuid().ToString("N")));

    private static IConfiguration CreateConfig(bool configured = true) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(configured
                ? new Dictionary<string, string?> { ["Email:Resend:ApiKey"] = "re_test_key" }
                : [])
            .Build();

    private static OutboundEmailService BuildService(AppDbContext db, FakeEmailSender sender, bool configured = true)
    {
        var businessDetails = new BusinessDetailsService(db, CreateStorage());
        return new(db,
            new EmailComposeService(db, businessDetails),
            new InvoicePdfService(db, businessDetails),
            sender,
            new EmailSettingsService(db, CreateConfig(configured)));
    }

    private static async Task<(Guid JobId, Guid InvoiceId)> SeedInvoiceAsync(AppDbContext db, string? customerEmail = "jane@example.com")
    {
        var customer = await new CustomerService(db).CreateAsync(
            new CreateCustomerRequest("Jane", "Doe", "0", customerEmail, null, null));
        var make = await new CarMakeService(db).CreateAsync(new CreateCarMakeRequest("Toyota"));
        var model = await new CarModelService(db).CreateAsync(new CreateCarModelRequest(make.Id, "Hilux"));
        var (car, _) = await new CarService(db).CreateAsync(
            new CreateCarRequest(customer.Id, make.Id, model!.Id, 2020, "ABC123", null, null, null));
        var jobs = new JobService(db);
        var (job, _) = await jobs.CreateAsync(new CreateJobRequest(customer.Id, car!.Id, "Brakes", JobStatus.Open, 1000, null, null));
        await new JobItemService(db, jobs).CreateAsync(job!.Id, new CreateJobItemRequest(
            "Pads", TradePrice: 100m, RetailPrice: 100m, MarkupSolution.Dollar, Markup: 10m, ItemQuantity: 1, SellingPrice: null));

        var invoices = new InvoiceService(db, new GstSettingService(db));
        var invoice = await invoices.GenerateAsync(job.Id, new CreateInvoiceRequest(null));
        return (job.Id, invoice!.Id);
    }

    private static SendInvoiceEmailRequest DefaultRequest(string to = "jane@example.com") =>
        new(to, "Jane Doe", null, "Your invoice", null);

    [Fact]
    public async Task SendInvoiceEmailAsync_WritesQueuedRow_BeforeCallingTheProvider()
    {
        await using var db = CreateContext();
        var (jobId, invoiceId) = await SeedInvoiceAsync(db);
        var sender = new FakeEmailSender();
        var service = BuildService(db, sender);

        var sawQueuedBeforeSend = false;
        sender.PreSendCallback = () =>
        {
            var row = db.OutboundEmails.AsNoTracking().Single();
            sawQueuedBeforeSend = row.Status == OutboundEmailStatus.Queued;
        };

        await service.SendInvoiceEmailAsync(jobId, invoiceId, DefaultRequest());

        Assert.True(sawQueuedBeforeSend);
    }

    [Fact]
    public async Task SendInvoiceEmailAsync_Success_SetsSentWithProviderMessageId()
    {
        await using var db = CreateContext();
        var (jobId, invoiceId) = await SeedInvoiceAsync(db);
        var sender = new FakeEmailSender();
        sender.EnqueueSendResult(new EmailSendResult(true, "provider-123", null));
        var service = BuildService(db, sender);

        var (email, error) = await service.SendInvoiceEmailAsync(jobId, invoiceId, DefaultRequest());

        Assert.Equal(EmailWriteError.None, error);
        Assert.Equal(OutboundEmailStatus.Sent, email!.Status);
        Assert.NotNull(email.SentAtUtc);
        var row = await db.OutboundEmails.SingleAsync();
        Assert.Equal("provider-123", row.ProviderMessageId);
        Assert.Equal(OutboundEmailKind.Invoice, row.Kind);
        Assert.Equal(invoiceId, row.InvoiceId);
    }

    [Fact]
    public async Task SendInvoiceEmailAsync_AttachesTheGeneratedPdf()
    {
        await using var db = CreateContext();
        var (jobId, invoiceId) = await SeedInvoiceAsync(db);
        var sender = new FakeEmailSender();
        sender.EnqueueSendResult(new EmailSendResult(true, "provider-123", null));
        var service = BuildService(db, sender);

        await service.SendInvoiceEmailAsync(jobId, invoiceId, DefaultRequest());

        var message = Assert.Single(sender.SentMessages);
        var attachment = Assert.Single(message.Attachments!);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.EndsWith(".pdf", attachment.FileName);
        Assert.True(attachment.Content.Length > 0);
    }

    [Fact]
    public async Task SendInvoiceEmailAsync_ProviderFailure_SetsFailedWithErrorMessage()
    {
        await using var db = CreateContext();
        var (jobId, invoiceId) = await SeedInvoiceAsync(db);
        var sender = new FakeEmailSender();
        sender.EnqueueSendResult(new EmailSendResult(false, null, "Invalid recipient"));
        var service = BuildService(db, sender);

        var (email, error) = await service.SendInvoiceEmailAsync(jobId, invoiceId, DefaultRequest());

        Assert.Equal(EmailWriteError.None, error); // the send attempt itself succeeded in being recorded
        Assert.Equal(OutboundEmailStatus.Failed, email!.Status);
        Assert.Equal("Invalid recipient", email.ErrorMessage);
        Assert.NotNull(email.FailedAtUtc);
    }

    [Fact]
    public async Task SendInvoiceEmailAsync_NotConfigured_WritesNoRow()
    {
        await using var db = CreateContext();
        var (jobId, invoiceId) = await SeedInvoiceAsync(db);
        var sender = new FakeEmailSender();
        var service = BuildService(db, sender, configured: false);

        var (email, error) = await service.SendInvoiceEmailAsync(jobId, invoiceId, DefaultRequest());

        Assert.Null(email);
        Assert.Equal(EmailWriteError.NotConfigured, error);
        Assert.Equal(0, await db.OutboundEmails.CountAsync());
        Assert.Empty(sender.SentMessages); // never even reached the provider
    }

    [Fact]
    public async Task SendInvoiceEmailAsync_InvoiceMissing_ReturnsInvoiceNotFound()
    {
        await using var db = CreateContext();
        var sender = new FakeEmailSender();
        var service = BuildService(db, sender);

        var (email, error) = await service.SendInvoiceEmailAsync(Guid.NewGuid(), Guid.NewGuid(), DefaultRequest());

        Assert.Null(email);
        Assert.Equal(EmailWriteError.InvoiceNotFound, error);
    }

    [Fact]
    public async Task ApplyStatusAsync_Delivered_AfterSent_Applies()
    {
        await using var db = CreateContext();
        var (jobId, invoiceId) = await SeedInvoiceAsync(db);
        var sender = new FakeEmailSender();
        sender.EnqueueSendResult(new EmailSendResult(true, "provider-1", null));
        var service = BuildService(db, sender);
        var (sent, _) = await service.SendInvoiceEmailAsync(jobId, invoiceId, DefaultRequest());

        await service.ApplyStatusAsync(sent!.Id, OutboundEmailStatus.Delivered, null, DateTime.UtcNow);

        var row = await db.OutboundEmails.SingleAsync();
        Assert.Equal(OutboundEmailStatus.Delivered, row.Status);
        Assert.NotNull(row.DeliveredAtUtc);
    }

    [Fact]
    public async Task ApplyStatusAsync_LateDelivered_CannotOverwriteBounced_StateMachineNeverRegresses()
    {
        await using var db = CreateContext();
        var (jobId, invoiceId) = await SeedInvoiceAsync(db);
        var sender = new FakeEmailSender();
        sender.EnqueueSendResult(new EmailSendResult(true, "provider-1", null));
        var service = BuildService(db, sender);
        var (sent, _) = await service.SendInvoiceEmailAsync(jobId, invoiceId, DefaultRequest());

        await service.ApplyStatusAsync(sent!.Id, OutboundEmailStatus.Bounced, "Mailbox full", DateTime.UtcNow);
        await service.ApplyStatusAsync(sent.Id, OutboundEmailStatus.Delivered, null, DateTime.UtcNow); // arrives late, out of order

        var row = await db.OutboundEmails.SingleAsync();
        Assert.Equal(OutboundEmailStatus.Bounced, row.Status);
        Assert.Equal("Mailbox full", row.ErrorMessage);
    }

    [Fact]
    public async Task ApplyStatusAsync_MissingRow_DoesNothing()
    {
        await using var db = CreateContext();
        var service = BuildService(db, new FakeEmailSender());

        await service.ApplyStatusAsync(Guid.NewGuid(), OutboundEmailStatus.Delivered, null, DateTime.UtcNow);

        Assert.Equal(0, await db.OutboundEmails.CountAsync());
    }

    [Fact]
    public async Task RetryAsync_OnlyAllowed_FromFailedOrBounced()
    {
        await using var db = CreateContext();
        var (jobId, invoiceId) = await SeedInvoiceAsync(db);
        var sender = new FakeEmailSender();
        sender.EnqueueSendResult(new EmailSendResult(true, "provider-1", null)); // this one succeeds -> Sent
        var service = BuildService(db, sender);
        var (sent, _) = await service.SendInvoiceEmailAsync(jobId, invoiceId, DefaultRequest());

        var (retried, error) = await service.RetryAsync(sent!.Id);

        Assert.Null(retried);
        Assert.Equal(EmailWriteError.NotRetryable, error);
    }

    [Fact]
    public async Task RetryAsync_FromFailed_CreatesNewRow_LeavingOriginalUntouched()
    {
        await using var db = CreateContext();
        var (jobId, invoiceId) = await SeedInvoiceAsync(db);
        var sender = new FakeEmailSender();
        sender.EnqueueSendResult(new EmailSendResult(false, null, "SMTP error"));
        sender.EnqueueSendResult(new EmailSendResult(true, "provider-2", null));
        var service = BuildService(db, sender);
        var (original, _) = await service.SendInvoiceEmailAsync(jobId, invoiceId, DefaultRequest());
        Assert.Equal(OutboundEmailStatus.Failed, original!.Status);

        var (retried, error) = await service.RetryAsync(original.Id);

        Assert.Equal(EmailWriteError.None, error);
        Assert.NotEqual(original.Id, retried!.Id);
        Assert.Equal(OutboundEmailStatus.Sent, retried.Status);
        Assert.Equal(2, await db.OutboundEmails.CountAsync());
        var originalRow = await db.OutboundEmails.SingleAsync(e => e.Id == original.Id);
        Assert.Equal(OutboundEmailStatus.Failed, originalRow.Status); // untouched

        // The PDF isn't stored on the row — retry must regenerate it, not just resend a stale copy.
        Assert.Equal(2, sender.SentMessages.Count);
        Assert.All(sender.SentMessages, m => Assert.NotNull(Assert.Single(m.Attachments!)));
    }

    [Fact]
    public async Task RetryAsync_MissingRow_ReturnsNotFound()
    {
        await using var db = CreateContext();
        var service = BuildService(db, new FakeEmailSender());

        var (email, error) = await service.RetryAsync(Guid.NewGuid());

        Assert.Null(email);
        Assert.Equal(EmailWriteError.NotFound, error);
    }

    [Fact]
    public async Task GetPendingStatusChecksAsync_ReturnsOnlySentRows_YoungerThan72Hours()
    {
        await using var db = CreateContext();
        var (jobId, invoiceId) = await SeedInvoiceAsync(db);
        var sender = new FakeEmailSender();
        sender.EnqueueSendResult(new EmailSendResult(true, "recent", null));
        var service = BuildService(db, sender);
        var (recent, _) = await service.SendInvoiceEmailAsync(jobId, invoiceId, DefaultRequest());

        // A second, older-than-72h Sent row, backdated directly via the context.
        var old = await db.OutboundEmails.SingleAsync(e => e.Id == recent!.Id);
        var stale = new OutboundEmail
        {
            ToAddress = "old@example.com", Subject = "Old", BodyHtml = "<p/>",
            Status = OutboundEmailStatus.Sent, ProviderMessageId = "stale",
            SentAtUtc = DateTime.UtcNow.AddHours(-100),
        };
        db.OutboundEmails.Add(stale);
        await db.SaveChangesAsync();

        var pending = await service.GetPendingStatusChecksAsync();

        Assert.Single(pending);
        Assert.Equal(old.Id, pending[0].Id);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersByInvoiceId()
    {
        await using var db = CreateContext();
        var (jobId, invoiceId) = await SeedInvoiceAsync(db);
        var (_, otherInvoiceId) = await SeedInvoiceAsync(db, "other@example.com");
        var sender = new FakeEmailSender();
        var service = BuildService(db, sender);
        await service.SendInvoiceEmailAsync(jobId, invoiceId, DefaultRequest());
        await service.SendInvoiceEmailAsync(jobId, otherInvoiceId, DefaultRequest("other@example.com"));

        var page = await service.GetPagedAsync(new OutboundEmailFilter(null, invoiceId, null, null));

        Assert.Equal(1, page.TotalCount);
        Assert.Equal(invoiceId, page.Items[0].InvoiceId);
    }

    [Fact]
    public async Task GetPreviewHtmlAsync_ReturnsStoredBody_OrNullWhenMissing()
    {
        await using var db = CreateContext();
        var (jobId, invoiceId) = await SeedInvoiceAsync(db);
        var sender = new FakeEmailSender();
        var service = BuildService(db, sender);
        var (sent, _) = await service.SendInvoiceEmailAsync(jobId, invoiceId, DefaultRequest());

        // The body is a short cover note now that the PDF carries the line-item detail.
        Assert.Contains("attached as a PDF", (await service.GetPreviewHtmlAsync(sent!.Id))!);
        Assert.Null(await service.GetPreviewHtmlAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task SendTestEmailAsync_Succeeds_WithTestKind_AndNoInvoiceOrCustomerLink()
    {
        await using var db = CreateContext();
        var sender = new FakeEmailSender();
        sender.EnqueueSendResult(new EmailSendResult(true, "provider-test", null));
        var service = BuildService(db, sender);

        var (email, error) = await service.SendTestEmailAsync("someone@example.com");

        Assert.Equal(EmailWriteError.None, error);
        Assert.Equal(OutboundEmailKind.Test, email!.Kind);
        Assert.Null(email.InvoiceId);
        Assert.Null(email.CustomerId);
        Assert.Null(Assert.Single(sender.SentMessages).Attachments); // no invoice to attach a PDF for
    }
}
