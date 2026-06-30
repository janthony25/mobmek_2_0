using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;
using JobService = MobmekApi.Services.JobService;

namespace MobmekApi.Tests.Services;

public class InvoiceServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    // Seeds a job carrying one item ($110), labour ($200) and one service line ($150) -> subtotal $460.
    private static async Task<(InvoiceService Invoices, JobService Jobs, Guid JobId)> SeedFullJobAsync(AppDbContext db)
    {
        var customer = await new CustomerService(db).CreateAsync(new CreateCustomerRequest("O", "P", "0", null, null, null));
        var make = await new CarMakeService(db).CreateAsync(new CreateCarMakeRequest("Make"));
        var model = await new CarModelService(db).CreateAsync(new CreateCarModelRequest(make.Id, "Model"));
        var (car, _) = await new CarService(db).CreateAsync(new CreateCarRequest(customer.Id, make.Id, model!.Id, 2020, "R", null, null, null, null));
        var jobs = new JobService(db);
        var (job, _) = await jobs.CreateAsync(new CreateJobRequest(customer.Id, car!.Id, "Brakes", JobStatus.Open, 1000, null, "Thanks!"));

        await new JobItemService(db, jobs).CreateAsync(job!.Id, new CreateJobItemRequest(
            "Pads", TradePrice: 100m, RetailPrice: null, MarkupSolution.Dollar, Markup: 10m, ItemQuantity: 1, SellingPrice: null)); // 110
        await new LabourService(db, jobs).CreateAsync(job.Id, new CreateLabourRequest(null, null, FixedAmount: 200m));               // 200
        var catalog = await new JobServiceCatalogService(db).CreateAsync(new CreateJobServiceRequest("Oil change", null, 50m, true));
        await new JobServiceLineService(db, jobs).CreateAsync(job.Id, new CreateJobServiceLineRequest(catalog.Id, 3));               // 150

        var invoices = new InvoiceService(db, new GstSettingService(db));
        return (invoices, jobs, job.Id);
    }

    [Fact]
    public async Task GenerateAsync_BuildsLines_AndSnapshotsTotalsAndGst()
    {
        await using var db = CreateContext();
        var (invoices, _, jobId) = await SeedFullJobAsync(db);

        var dueDate = new DateOnly(2026, 7, 31);
        var invoice = await invoices.GenerateAsync(jobId, new CreateInvoiceRequest(dueDate, "Card", "Net 14"));

        Assert.NotNull(invoice);
        Assert.Equal(dueDate, invoice!.DueDate);
        Assert.Equal("Card", invoice.ModeOfPayment);
        Assert.Equal("Net 14", invoice.PaymentTerm);
        Assert.Equal("Brakes", invoice.IssueName);
        Assert.Equal("Thanks!", invoice.Notes);
        Assert.Equal("Invoice", invoice.DocumentType);
        Assert.Equal("Active", invoice.Status);
        Assert.Equal(200m, invoice.LabourPrice);
        Assert.Equal(460m, invoice.SubTotal);          // 110 + 200 + 150
        Assert.Equal(0.15m, invoice.GstRate);          // default
        Assert.Equal(69m, invoice.TaxAmount);          // 460 * 0.15 (inclusive, display only)
        Assert.Equal(460m, invoice.TotalAmount);       // tax is inclusive, not added on top

        // One line per item + one consolidated labour line + one per service line.
        Assert.Equal(3, invoice.Items.Count);
        Assert.Contains(invoice.Items, i => i.ItemName == "Pads" && i.ItemTotal == 110m);
        Assert.Contains(invoice.Items, i => i.ItemName == "Labour" && i.ItemTotal == 200m);
        Assert.Contains(invoice.Items, i => i.ItemName == "Oil change" && i.ItemTotal == 150m);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsNull_WhenJobMissing()
    {
        await using var db = CreateContext();
        var invoices = new InvoiceService(db, new GstSettingService(db));

        var invoice = await invoices.GenerateAsync(Guid.NewGuid(), new CreateInvoiceRequest(null, null, null));

        Assert.Null(invoice);
    }

    [Fact]
    public async Task GenerateAsync_UsesCurrentGstRate_AndSnapshotSurvivesLaterRateChange()
    {
        await using var db = CreateContext();
        var (invoices, _, jobId) = await SeedFullJobAsync(db);
        await new GstSettingService(db).UpdateAsync(0.10m);

        var invoice = await invoices.GenerateAsync(jobId, new CreateInvoiceRequest(null, null, null));
        Assert.Equal(0.10m, invoice!.GstRate);
        Assert.Equal(46m, invoice.TaxAmount);          // 460 * 0.10

        // Changing the rate afterwards must not alter the already-issued invoice.
        await new GstSettingService(db).UpdateAsync(0.20m);
        var refetched = await invoices.GetByIdAsync(jobId, invoice.Id);
        Assert.Equal(0.10m, refetched!.GstRate);
        Assert.Equal(46m, refetched.TaxAmount);
    }

    [Fact]
    public async Task RejectAsync_SetsStatusRejected_WithoutDeleting()
    {
        await using var db = CreateContext();
        var (invoices, _, jobId) = await SeedFullJobAsync(db);
        var invoice = await invoices.GenerateAsync(jobId, new CreateInvoiceRequest(null, null, null));

        var rejected = await invoices.RejectAsync(jobId, invoice!.Id);

        Assert.Equal("Rejected", rejected!.Status);
        Assert.NotNull(await invoices.GetByIdAsync(jobId, invoice.Id)); // still retrievable
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenInvoiceBelongsToAnotherJob()
    {
        await using var db = CreateContext();
        var (invoices, jobs, jobId) = await SeedFullJobAsync(db);
        var seededJob = await db.Jobs.AsNoTracking().FirstAsync();
        var (otherJob, _) = await jobs.CreateAsync(new CreateJobRequest(
            seededJob.CustomerId, seededJob.CarId, "Other", JobStatus.Open, 0, null, null));
        var invoice = await invoices.GenerateAsync(jobId, new CreateInvoiceRequest(null, null, null));

        Assert.Null(await invoices.GetByIdAsync(otherJob!.Id, invoice!.Id));
        Assert.Null(await invoices.RejectAsync(otherJob.Id, invoice.Id));
    }
}
