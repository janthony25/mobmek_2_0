using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;
using JobService = MobmekApi.Services.JobService;

namespace MobmekApi.Tests.Services;

public class InvoicePdfServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static IFileStorage CreateStorage() =>
        new LocalFileStorage(Path.Combine(Path.GetTempPath(), "mobmek-tests", Guid.NewGuid().ToString("N")));

    private static async Task<(InvoicePdfService Pdf, InvoiceService Invoices, Guid JobId)> SeedJobAsync(AppDbContext db)
    {
        var customer = await new CustomerService(db).CreateAsync(
            new CreateCustomerRequest("Jane", "Doe", "0", "jane@example.com", null, null));
        var make = await new CarMakeService(db).CreateAsync(new CreateCarMakeRequest("Toyota"));
        var model = await new CarModelService(db).CreateAsync(new CreateCarModelRequest(make.Id, "Hilux"));
        var (car, _) = await new CarService(db).CreateAsync(
            new CreateCarRequest(customer.Id, make.Id, model!.Id, 2020, "ABC123", null, null, null));
        var jobs = new JobService(db);
        var (job, _) = await jobs.CreateAsync(new CreateJobRequest(customer.Id, car!.Id, "Brakes", JobStatus.Open, 1000, null, null));
        await new JobItemService(db, jobs).CreateAsync(job!.Id, new CreateJobItemRequest(
            "Pads", TradePrice: 100m, RetailPrice: 100m, MarkupSolution.Dollar, Markup: 10m, ItemQuantity: 1, SellingPrice: null));

        var invoices = new InvoiceService(db, new GstSettingService(db));
        var pdf = new InvoicePdfService(db, new BusinessDetailsService(db, CreateStorage()));
        return (pdf, invoices, job.Id);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsNull_WhenInvoiceMissing()
    {
        await using var db = CreateContext();
        var pdf = new InvoicePdfService(db, new BusinessDetailsService(db, CreateStorage()));

        var result = await pdf.GenerateAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsValidPdfBytes_ForInvoice()
    {
        await using var db = CreateContext();
        var (pdf, invoices, jobId) = await SeedJobAsync(db);
        var invoice = await invoices.GenerateAsync(jobId, new CreateInvoiceRequest(new DateOnly(2026, 8, 1)));

        var result = await pdf.GenerateAsync(jobId, invoice!.Id);

        Assert.NotNull(result);
        Assert.True(result!.Bytes.Length > 0);
        // %PDF is the magic header of every valid PDF file.
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(result.Bytes, 0, 4));
        Assert.Equal($"{invoice.InvoiceNumber}.pdf", result.FileName);
    }

    [Fact]
    public async Task GenerateAsync_UsesQuoPrefix_ForQuotation()
    {
        await using var db = CreateContext();
        var (pdf, invoices, jobId) = await SeedJobAsync(db);
        var quotation = await invoices.GenerateQuotationAsync(jobId, new CreateInvoiceRequest(null));

        var result = await pdf.GenerateAsync(jobId, quotation!.Id);

        Assert.NotNull(result);
        Assert.StartsWith("QUO-", result!.FileName);
    }

    [Fact]
    public async Task GenerateAsync_DoesNotThrow_WhenBusinessHasNoLogo()
    {
        await using var db = CreateContext();
        var (pdf, invoices, jobId) = await SeedJobAsync(db);
        var invoice = await invoices.GenerateAsync(jobId, new CreateInvoiceRequest(null));

        // BusinessDetailsService default row has no logo set — this must not throw.
        var result = await pdf.GenerateAsync(jobId, invoice!.Id);

        Assert.NotNull(result);
    }
}
