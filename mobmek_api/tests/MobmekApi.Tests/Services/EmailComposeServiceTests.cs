using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;
using JobService = MobmekApi.Services.JobService;

namespace MobmekApi.Tests.Services;

public class EmailComposeServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static IFileStorage CreateStorage() =>
        new LocalFileStorage(Path.Combine(Path.GetTempPath(), "mobmek-tests", Guid.NewGuid().ToString("N")));

    // One job for "Jane Doe" in a Toyota Hilux, one invoice with a single $110 line.
    private static async Task<(EmailComposeService Compose, Guid JobId, Guid InvoiceId)> SeedInvoiceAsync(
        AppDbContext db, string? customerEmail = "jane@example.com")
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
        var invoice = await invoices.GenerateAsync(job.Id, new CreateInvoiceRequest(new DateOnly(2026, 8, 1)));

        var compose = new EmailComposeService(db, new BusinessDetailsService(db, CreateStorage()));
        return (compose, job.Id, invoice!.Id);
    }

    [Fact]
    public async Task ComposeInvoiceEmailAsync_ReturnsNull_WhenInvoiceMissing()
    {
        await using var db = CreateContext();
        var compose = new EmailComposeService(db, new BusinessDetailsService(db, CreateStorage()));

        var draft = await compose.ComposeInvoiceEmailAsync(Guid.NewGuid(), Guid.NewGuid(), null);

        Assert.Null(draft);
    }

    [Fact]
    public async Task ComposeInvoiceEmailAsync_DefaultsRecipient_FromCustomerOnFile()
    {
        await using var db = CreateContext();
        var (compose, jobId, invoiceId) = await SeedInvoiceAsync(db);

        var draft = await compose.ComposeInvoiceEmailAsync(jobId, invoiceId, null);

        Assert.NotNull(draft);
        Assert.Equal("jane@example.com", draft!.DefaultToAddress);
        Assert.Equal("Jane Doe", draft.DefaultToName);
        Assert.NotNull(draft.CustomerId);
    }

    [Fact]
    public async Task ComposeInvoiceEmailAsync_NullRecipient_WhenCustomerHasNoEmailOnFile()
    {
        await using var db = CreateContext();
        var (compose, jobId, invoiceId) = await SeedInvoiceAsync(db, customerEmail: null);

        var draft = await compose.ComposeInvoiceEmailAsync(jobId, invoiceId, null);

        Assert.NotNull(draft);
        Assert.Null(draft!.DefaultToAddress);
    }

    [Fact]
    public async Task ComposeInvoiceEmailAsync_BodyContains_LetterheadTotalAndBankDetails()
    {
        await using var db = CreateContext();
        var storage = CreateStorage();
        var businessDetails = new BusinessDetailsService(db, storage);
        await businessDetails.UpdateAsync(new UpdateBusinessDetailsRequest(
            "Jun Garage", "1 Main St", "shop@jungarage.co.nz", "0400 000 000", null,
            "12 345 678 901", null, "Account: Jun Garage\nBank: ANZ\n12-3456-7890123-00"));

        var customer = await new CustomerService(db).CreateAsync(
            new CreateCustomerRequest("Jane", "Doe", "0", "jane@example.com", null, null));
        var make = await new CarMakeService(db).CreateAsync(new CreateCarMakeRequest("Toyota"));
        var model = await new CarModelService(db).CreateAsync(new CreateCarModelRequest(make.Id, "Hilux"));
        var (car, _) = await new CarService(db).CreateAsync(
            new CreateCarRequest(customer.Id, make.Id, model!.Id, 2020, "ABC123", null, null, null));
        var jobs = new JobService(db);
        var (job, _) = await jobs.CreateAsync(new CreateJobRequest(customer.Id, car!.Id, "Brakes", JobStatus.Open, 1000, null, null));
        await new JobItemService(db, jobs).CreateAsync(job!.Id, new CreateJobItemRequest(
            "Brake Pads", TradePrice: 100m, RetailPrice: 100m, MarkupSolution.Dollar, Markup: 10m, ItemQuantity: 1, SellingPrice: null));
        var invoices = new InvoiceService(db, new GstSettingService(db));
        var invoice = await invoices.GenerateAsync(job.Id, new CreateInvoiceRequest(null));

        var compose = new EmailComposeService(db, businessDetails);
        var draft = await compose.ComposeInvoiceEmailAsync(job.Id, invoice!.Id, "Thanks for your business!");

        Assert.NotNull(draft);
        var html = draft!.BodyHtml;
        Assert.Contains("Jun Garage", html);
        Assert.Contains("12 345 678 901", html);
        // Line items now live only in the PDF attachment (InvoicePdfService) — the body is a
        // short cover note pointing at it, not a duplicate of the detail.
        Assert.Contains("attached as a PDF", html);
        Assert.DoesNotContain("Brake Pads", html);
        Assert.Contains("Thanks for your business!", html);
        Assert.Contains("ANZ", html);
        Assert.Contains(invoice.TotalAmount.ToString("N2"), html);
    }

    [Fact]
    public async Task ComposeInvoiceEmailAsync_Subject_IncludesDocumentNumberAndBusinessName()
    {
        await using var db = CreateContext();
        var (compose, jobId, invoiceId) = await SeedInvoiceAsync(db);

        var draft = await compose.ComposeInvoiceEmailAsync(jobId, invoiceId, null);

        Assert.Contains("INV-", draft!.Subject);
        Assert.Contains("Mobmek Workshop", draft.Subject);
    }
}
