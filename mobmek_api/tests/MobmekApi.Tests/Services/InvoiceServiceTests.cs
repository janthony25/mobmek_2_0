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
            "Pads", TradePrice: 100m, RetailPrice: 100m, MarkupSolution.Dollar, Markup: 10m, ItemQuantity: 1, SellingPrice: null)); // 110
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
        var invoice = await invoices.GenerateAsync(jobId, new CreateInvoiceRequest(dueDate));

        Assert.NotNull(invoice);
        Assert.Equal(dueDate, invoice!.DueDate);
        Assert.Null(invoice.ModeOfPayment);             // only known once the customer pays
        Assert.Null(invoice.PaymentTerm);
        Assert.Equal("Brakes", invoice.IssueName);
        Assert.Equal("Thanks!", invoice.Notes);
        Assert.Equal("Invoice", invoice.DocumentType);
        Assert.Equal("Active", invoice.Status);
        Assert.Equal(200m, invoice.LabourPrice);
        Assert.Equal(460m, invoice.SubTotal);          // 110 + 200 + 150
        Assert.Equal(0.15m, invoice.GstRate);          // default
        Assert.Equal(69m, invoice.TaxAmount);          // 460 * 0.15
        Assert.Equal(529m, invoice.TotalAmount);       // 460 subtotal + 69 GST added on top

        // One line per item + one consolidated labour line + one per service line.
        Assert.Equal(3, invoice.Items.Count);
        Assert.Contains(invoice.Items, i => i.ItemName == "Pads" && i.ItemTotal == 110m);
        Assert.Contains(invoice.Items, i => i.ItemName == "Labour" && i.ItemTotal == 200m);
        Assert.Contains(invoice.Items, i => i.ItemName == "Oil change" && i.ItemTotal == 150m);
    }

    [Fact]
    public async Task GenerateAsync_AssignsSequentialInvoiceNumbers()
    {
        await using var db = CreateContext();
        var (invoices, _, jobId) = await SeedFullJobAsync(db);

        var first = await invoices.GenerateAsync(jobId, new CreateInvoiceRequest(null));
        var second = await invoices.GenerateAsync(jobId, new CreateInvoiceRequest(null));

        Assert.Equal("INV-0001", first!.InvoiceNumber);
        Assert.Equal("INV-0002", second!.InvoiceNumber);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsNull_WhenJobMissing()
    {
        await using var db = CreateContext();
        var invoices = new InvoiceService(db, new GstSettingService(db));

        var invoice = await invoices.GenerateAsync(Guid.NewGuid(), new CreateInvoiceRequest(null));

        Assert.Null(invoice);
    }

    [Fact]
    public async Task GenerateAsync_UsesCurrentGstRate_AndSnapshotSurvivesLaterRateChange()
    {
        await using var db = CreateContext();
        var (invoices, _, jobId) = await SeedFullJobAsync(db);
        await new GstSettingService(db).UpdateAsync(0.10m);

        var invoice = await invoices.GenerateAsync(jobId, new CreateInvoiceRequest(null));
        Assert.Equal(0.10m, invoice!.GstRate);
        Assert.Equal(46m, invoice.TaxAmount);          // 460 * 0.10

        // Changing the rate afterwards must not alter the already-issued invoice.
        await new GstSettingService(db).UpdateAsync(0.20m);
        var refetched = await invoices.GetByIdAsync(jobId, invoice.Id);
        Assert.Equal(0.10m, refetched!.GstRate);
        Assert.Equal(46m, refetched.TaxAmount);
    }

    [Fact]
    public async Task GenerateAsync_LeavesInvoiceUnpaid()
    {
        await using var db = CreateContext();
        var (invoices, _, jobId) = await SeedFullJobAsync(db);

        var invoice = await invoices.GenerateAsync(jobId, new CreateInvoiceRequest(null));

        Assert.False(invoice!.IsPaid);
        Assert.Null(invoice.AmountPaid);
        Assert.Null(invoice.DatePaid);
        Assert.Null(invoice.CashAmount);
        Assert.Null(invoice.CardAmount);
    }

    [Fact]
    public async Task MarkPaidAsync_StampsPayment_WithModeOfPaymentTermAndCashCardSplit()
    {
        await using var db = CreateContext();
        var (invoices, _, jobId) = await SeedFullJobAsync(db);
        var invoice = await invoices.GenerateAsync(jobId, new CreateInvoiceRequest(null));

        var datePaid = new DateOnly(2026, 7, 1);
        var paid = await invoices.MarkPaidAsync(jobId, invoice!.Id, new MarkInvoicePaidRequest("Card", "Net 14", 400m, 129m, datePaid));

        Assert.True(paid!.IsPaid);
        Assert.Equal(datePaid, paid.DatePaid);
        Assert.Equal(529m, paid.AmountPaid);           // equals the total (460 + 69 GST)
        Assert.Equal(400m, paid.CashAmount);
        Assert.Equal(129m, paid.CardAmount);
        Assert.Equal("Card", paid.ModeOfPayment);
        Assert.Equal("Net 14", paid.PaymentTerm);
    }

    [Fact]
    public async Task MarkPaidAsync_DefaultsDatePaidToToday_WhenOmitted()
    {
        await using var db = CreateContext();
        var (invoices, _, jobId) = await SeedFullJobAsync(db);
        var invoice = await invoices.GenerateAsync(jobId, new CreateInvoiceRequest(null));

        var paid = await invoices.MarkPaidAsync(jobId, invoice!.Id, new MarkInvoicePaidRequest(null, null, null, null, null));

        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), paid!.DatePaid);
    }

    [Fact]
    public async Task MarkPaidAsync_ReturnsNull_WhenInvoiceBelongsToAnotherJobOrMissing()
    {
        await using var db = CreateContext();
        var (invoices, jobs, jobId) = await SeedFullJobAsync(db);
        var seededJob = await db.Jobs.AsNoTracking().FirstAsync();
        var (otherJob, _) = await jobs.CreateAsync(new CreateJobRequest(
            seededJob.CustomerId, seededJob.CarId, "Other", JobStatus.Open, 0, null, null));
        var invoice = await invoices.GenerateAsync(jobId, new CreateInvoiceRequest(null));

        Assert.Null(await invoices.MarkPaidAsync(otherJob!.Id, invoice!.Id, new MarkInvoicePaidRequest(null, null, null, null, null)));
        Assert.Null(await invoices.MarkPaidAsync(jobId, Guid.NewGuid(), new MarkInvoicePaidRequest(null, null, null, null, null)));
    }

    [Fact]
    public async Task MarkPaidAsync_ReturnsNull_WhenInvoiceRejected()
    {
        await using var db = CreateContext();
        var (invoices, _, jobId) = await SeedFullJobAsync(db);
        var invoice = await invoices.GenerateAsync(jobId, new CreateInvoiceRequest(null));
        await invoices.RejectAsync(jobId, invoice!.Id);

        var paid = await invoices.MarkPaidAsync(jobId, invoice.Id, new MarkInvoicePaidRequest(null, null, null, null, null));

        Assert.Null(paid);
        var refetched = await invoices.GetByIdAsync(jobId, invoice.Id);
        Assert.False(refetched!.IsPaid);               // still unpaid, still rejected
        Assert.Equal("Rejected", refetched.Status);
    }

    [Fact]
    public async Task RejectAsync_SetsStatusRejected_WithoutDeleting()
    {
        await using var db = CreateContext();
        var (invoices, _, jobId) = await SeedFullJobAsync(db);
        var invoice = await invoices.GenerateAsync(jobId, new CreateInvoiceRequest(null));

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
        var invoice = await invoices.GenerateAsync(jobId, new CreateInvoiceRequest(null));

        Assert.Null(await invoices.GetByIdAsync(otherJob!.Id, invoice!.Id));
        Assert.Null(await invoices.RejectAsync(otherJob.Id, invoice.Id));
    }

    // --- Cash-flow ledger posting on payment ---

    // Bank as default + card route, the till for cash, savings for bank transfers.
    private static async Task<(CashAccountDto Bank, CashAccountDto Till, CashAccountDto Savings)> SeedLedgerRoutingAsync(AppDbContext db)
    {
        var accounts = new CashAccountService(db);
        var bank = await accounts.CreateAsync(new CreateCashAccountRequest("Bank", "Bank", null, 0m, new DateOnly(2026, 1, 1)));
        var till = await accounts.CreateAsync(new CreateCashAccountRequest("Till", "Cash", null, 0m, new DateOnly(2026, 1, 1)));
        var savings = await accounts.CreateAsync(new CreateCashAccountRequest("Savings", "Bank", null, 0m, new DateOnly(2026, 1, 1)));
        await new CashFlowSettingsService(db).UpdateAsync(new UpdateCashFlowSettingsRequest(bank.Id, till.Id, bank.Id, savings.Id));
        return (bank, till, savings);
    }

    [Fact]
    public async Task MarkPaidAsync_PostsRoutedLedgerLegs_ForCashCardSplit()
    {
        await using var db = CreateContext();
        var (invoices, _, jobId) = await SeedFullJobAsync(db);
        var (bank, till, _) = await SeedLedgerRoutingAsync(db);
        var invoice = await invoices.GenerateAsync(jobId, new CreateInvoiceRequest(null));

        var datePaid = new DateOnly(2026, 7, 1);
        await invoices.MarkPaidAsync(jobId, invoice!.Id, new MarkInvoicePaidRequest("Split", null, 400m, 129m, datePaid));

        var postings = await db.CashTransactions.Where(t => t.InvoiceId == invoice.Id).ToListAsync();
        Assert.Equal(2, postings.Count);
        Assert.Contains(postings, t => t.AccountId == till.Id && t.Amount == 400m);   // cash → till
        Assert.Contains(postings, t => t.AccountId == bank.Id && t.Amount == 129m);   // card → bank
        Assert.All(postings, t =>
        {
            Assert.Equal("In", t.Direction);
            Assert.Equal(datePaid, t.Date);
            Assert.Equal("Invoice INV-0001 — Brakes", t.Description);
            Assert.Equal("O P", t.Counterparty);                                      // the job's customer
        });
        var category = await db.TransactionCategories.SingleAsync(c => c.Id == postings[0].CategoryId);
        Assert.Equal(CashFlowSeeder.WorkshopSalesCategory, category.Name);
    }

    [Fact]
    public async Task MarkPaidAsync_RoutesFullAmountByModeOfPayment()
    {
        await using var db = CreateContext();
        var (invoices, _, jobId) = await SeedFullJobAsync(db);
        var (_, _, savings) = await SeedLedgerRoutingAsync(db);
        var invoice = await invoices.GenerateAsync(jobId, new CreateInvoiceRequest(null));

        await invoices.MarkPaidAsync(jobId, invoice!.Id, new MarkInvoicePaidRequest("Bank Transfer", null, null, null, null));

        var posting = await db.CashTransactions.SingleAsync(t => t.InvoiceId == invoice.Id);
        Assert.Equal(savings.Id, posting.AccountId);
        Assert.Equal(529m, posting.Amount);            // the whole total in one leg
    }

    [Fact]
    public async Task MarkPaidAsync_FallsBackToDefaultAccount_ForUnknownMode()
    {
        await using var db = CreateContext();
        var (invoices, _, jobId) = await SeedFullJobAsync(db);
        var (bank, _, _) = await SeedLedgerRoutingAsync(db);
        var invoice = await invoices.GenerateAsync(jobId, new CreateInvoiceRequest(null));

        await invoices.MarkPaidAsync(jobId, invoice!.Id, new MarkInvoicePaidRequest("Cheque", null, null, null, null));

        var posting = await db.CashTransactions.SingleAsync(t => t.InvoiceId == invoice.Id);
        Assert.Equal(bank.Id, posting.AccountId);
    }

    [Fact]
    public async Task MarkPaidAsync_SkipsLedger_WhenNoRoutingConfigured()
    {
        await using var db = CreateContext();
        var (invoices, _, jobId) = await SeedFullJobAsync(db);
        var invoice = await invoices.GenerateAsync(jobId, new CreateInvoiceRequest(null));

        var paid = await invoices.MarkPaidAsync(jobId, invoice!.Id, new MarkInvoicePaidRequest("Cash", null, null, null, null));

        Assert.True(paid!.IsPaid);                     // payment itself still succeeds
        Assert.Empty(await db.CashTransactions.ToListAsync());
    }

    [Fact]
    public async Task MarkPaidAsync_Twice_ReplacesEarlierPostingsInsteadOfDoubling()
    {
        await using var db = CreateContext();
        var (invoices, _, jobId) = await SeedFullJobAsync(db);
        var (_, till, _) = await SeedLedgerRoutingAsync(db);
        var invoice = await invoices.GenerateAsync(jobId, new CreateInvoiceRequest(null));

        await invoices.MarkPaidAsync(jobId, invoice!.Id, new MarkInvoicePaidRequest("Split", null, 400m, 129m, null));
        await invoices.MarkPaidAsync(jobId, invoice.Id, new MarkInvoicePaidRequest("Cash", null, null, null, null));

        var posting = await db.CashTransactions.SingleAsync(t => t.InvoiceId == invoice.Id);
        Assert.Equal(till.Id, posting.AccountId);      // only the latest payment's posting remains
        Assert.Equal(529m, posting.Amount);
    }

    [Fact]
    public async Task RejectAsync_RemovesLedgerPostings_OfAPaidInvoice()
    {
        await using var db = CreateContext();
        var (invoices, _, jobId) = await SeedFullJobAsync(db);
        await SeedLedgerRoutingAsync(db);
        var invoice = await invoices.GenerateAsync(jobId, new CreateInvoiceRequest(null));
        await invoices.MarkPaidAsync(jobId, invoice!.Id, new MarkInvoicePaidRequest("Cash", null, null, null, null));
        Assert.NotEmpty(await db.CashTransactions.Where(t => t.InvoiceId == invoice.Id).ToListAsync());

        await invoices.RejectAsync(jobId, invoice.Id);

        Assert.Empty(await db.CashTransactions.Where(t => t.InvoiceId == invoice.Id).ToListAsync());
    }
}
