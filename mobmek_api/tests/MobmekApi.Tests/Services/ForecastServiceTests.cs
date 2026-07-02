using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class ForecastServiceTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static async Task<Guid> SeedInvoiceAsync(
        AppDbContext db, Guid customerId, DateOnly? dueDate, decimal total, bool isPaid = false, DateOnly? datePaid = null)
    {
        var job = new Job { CustomerId = customerId, CarId = Guid.NewGuid(), Title = "Job" };
        db.Jobs.Add(job);

        var invoice = new Invoice
        {
            JobId = job.Id,
            IssueName = "Invoice",
            SequenceNumber = 1,
            DueDate = dueDate,
            TotalAmount = total,
            IsPaid = isPaid,
            DatePaid = datePaid,
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        return invoice.Id;
    }

    [Fact]
    public async Task ProjectAsync_OpeningBalanceExcludesArchivedAccounts()
    {
        await using var db = CreateContext();
        var accounts = new CashAccountService(db);
        var bank = await accounts.CreateAsync(new CreateCashAccountRequest("Bank", "Bank", null, 1000m, new DateOnly(2026, 1, 1)));
        var till = await accounts.CreateAsync(new CreateCashAccountRequest("Till", "Cash", null, 500m, new DateOnly(2026, 1, 1)));
        await accounts.UpdateAsync(till.Id, new UpdateCashAccountRequest("Till", "Cash", null, 500m, new DateOnly(2026, 1, 1), IsArchived: true));

        var forecast = new ForecastService(db, accounts);
        var result = await forecast.ProjectAsync(30, "Expected");

        Assert.Equal(1000m, result.OpeningBalance);
    }

    [Fact]
    public async Task ProjectAsync_Expected_PlacesReceivableAtDueDatePlusCustomerMedianLag()
    {
        await using var db = CreateContext();
        var accounts = new CashAccountService(db);
        await accounts.CreateAsync(new CreateCashAccountRequest("Bank", "Bank", null, 0m, new DateOnly(2026, 1, 1)));
        var customerId = Guid.NewGuid();

        // Two paid invoices with lags of 5 and 3 days -> median 4.
        await SeedInvoiceAsync(db, customerId, new DateOnly(2026, 1, 1), 100m, isPaid: true, datePaid: new DateOnly(2026, 1, 6));
        await SeedInvoiceAsync(db, customerId, new DateOnly(2026, 2, 1), 100m, isPaid: true, datePaid: new DateOnly(2026, 2, 4));
        await SeedInvoiceAsync(db, customerId, Today.AddDays(10), 1000m);

        var forecast = new ForecastService(db, accounts);
        var result = await forecast.ProjectAsync(30, "Expected");

        var expectedDate = Today.AddDays(10 + 4);
        var point = result.DailyPoints.Single(p => p.Date == expectedDate);
        Assert.Equal(1000m, point.In);
        Assert.DoesNotContain(result.DailyPoints, p => p.Date == Today.AddDays(10) && p.In > 0);
    }

    [Fact]
    public async Task ProjectAsync_BestCase_CollectsFullAmountOnDueDateIgnoringLag()
    {
        await using var db = CreateContext();
        var accounts = new CashAccountService(db);
        await accounts.CreateAsync(new CreateCashAccountRequest("Bank", "Bank", null, 0m, new DateOnly(2026, 1, 1)));
        var customerId = Guid.NewGuid();
        await SeedInvoiceAsync(db, customerId, new DateOnly(2026, 1, 1), 100m, isPaid: true, datePaid: new DateOnly(2026, 1, 6));
        await SeedInvoiceAsync(db, customerId, Today.AddDays(10), 1000m);

        var forecast = new ForecastService(db, accounts);
        var result = await forecast.ProjectAsync(30, "BestCase");

        var point = result.DailyPoints.Single(p => p.Date == Today.AddDays(10));
        Assert.Equal(1000m, point.In);
    }

    [Fact]
    public async Task ProjectAsync_WorstCase_Discounts15PercentAndAddsFourteenExtraDays()
    {
        await using var db = CreateContext();
        var accounts = new CashAccountService(db);
        await accounts.CreateAsync(new CreateCashAccountRequest("Bank", "Bank", null, 0m, new DateOnly(2026, 1, 1)));
        var customerId = Guid.NewGuid();
        await SeedInvoiceAsync(db, customerId, new DateOnly(2026, 1, 1), 100m, isPaid: true, datePaid: new DateOnly(2026, 1, 6)); // lag 5
        await SeedInvoiceAsync(db, customerId, Today.AddDays(10), 1000m);

        var forecast = new ForecastService(db, accounts);
        var result = await forecast.ProjectAsync(45, "WorstCase");

        var expectedDate = Today.AddDays(10 + 5 + 14);
        var point = result.DailyPoints.Single(p => p.Date == expectedDate);
        Assert.Equal(850m, point.In);
    }

    [Fact]
    public async Task ProjectAsync_Recurring_ExcludesAlreadyPostedAndAppliesScenarioMultiplier()
    {
        await using var db = CreateContext();
        var accounts = new CashAccountService(db);
        var bank = await accounts.CreateAsync(new CreateCashAccountRequest("Bank", "Bank", null, 0m, new DateOnly(2026, 1, 1)));
        var (category, _) = await new TransactionCategoryService(db).CreateAsync(
            new CreateTransactionCategoryRequest("Software Subscriptions", "Out", "Operating", null, false));
        var recurringService = new RecurringTransactionService(db);
        var (schedule, _) = await recurringService.CreateAsync(new CreateRecurringTransactionRequest(
            "Xero", "Out", 100m, category!.Id, bank.Id, null, null, "Monthly", 1, Today, null, false));
        await recurringService.PostOccurrenceAsync(schedule!.Id, Today); // today's occurrence is already posted

        var forecast = new ForecastService(db, accounts);
        var result = await forecast.ProjectAsync(60, "BestCase");

        Assert.DoesNotContain(result.DailyPoints, p => p.Date == Today && p.Out > 0);
        var nextOccurrence = Today.AddMonths(1);
        var point = result.DailyPoints.Single(p => p.Date == nextOccurrence);
        Assert.Equal(95m, point.Out); // 100 * 0.95 BestCase expense multiplier
    }

    [Fact]
    public async Task ProjectAsync_Planned_IncludedByScenarioTagMatch()
    {
        await using var db = CreateContext();
        var accounts = new CashAccountService(db);
        var bank = await accounts.CreateAsync(new CreateCashAccountRequest("Bank", "Bank", null, 0m, new DateOnly(2026, 1, 1)));
        var (category, _) = await new TransactionCategoryService(db).CreateAsync(
            new CreateTransactionCategoryRequest("Tools & Equipment", "Out", "Operating", null, false));
        var plannedService = new PlannedTransactionService(db);
        var date = Today.AddDays(5);
        await plannedService.CreateAsync(new CreatePlannedTransactionRequest("Always item", "Out", 200m, date, category!.Id, bank.Id, null));
        await plannedService.CreateAsync(new CreatePlannedTransactionRequest("Best-case item", "Out", 300m, date, category.Id, bank.Id, "BestCase"));
        await plannedService.CreateAsync(new CreatePlannedTransactionRequest("Worst-case item", "Out", 400m, date, category.Id, bank.Id, "WorstCase"));

        var forecast = new ForecastService(db, accounts);
        var expected = await forecast.ProjectAsync(30, "Expected");
        var bestCase = await forecast.ProjectAsync(30, "BestCase");

        Assert.Equal(200m, expected.DailyPoints.Single(p => p.Date == date).Out);
        Assert.Equal(500m, bestCase.DailyPoints.Single(p => p.Date == date).Out); // 200 Always + 300 BestCase
    }

    [Fact]
    public async Task ProjectAsync_ShortageDate_AlwaysReflectsExpectedRegardlessOfRequestedScenario()
    {
        await using var db = CreateContext();
        var accounts = new CashAccountService(db);
        var bank = await accounts.CreateAsync(new CreateCashAccountRequest("Bank", "Bank", null, 1000m, new DateOnly(2026, 1, 1)));
        var (category, _) = await new TransactionCategoryService(db).CreateAsync(
            new CreateTransactionCategoryRequest("Rent", "Out", "Operating", null, false));
        await new RecurringTransactionService(db).CreateAsync(new CreateRecurringTransactionRequest(
            "Rent", "Out", 200m, category!.Id, bank.Id, null, null, "Monthly", 1, Today, null, false));
        await new CashFlowSettingsService(db, new CashFlowAuditService(db)).UpdateAsync(new UpdateCashFlowSettingsRequest(null, null, null, null, 805m, null));

        var forecast = new ForecastService(db, accounts);
        var bestCaseResult = await forecast.ProjectAsync(30, "BestCase");

        // Expected drops to 1000-200=800 (< buffer 805) on day one; BestCase itself only drops to
        // 1000-190=810 (>= buffer), so the shortage date can only have come from the Expected series.
        Assert.Equal(Today, bestCaseResult.ShortageDate);
        Assert.Equal(810m, bestCaseResult.DailyPoints.Single(p => p.Date == Today).ClosingBalance);
    }

    [Fact]
    public async Task ProjectAsync_DailyResolutionCapsAtNinetyDays_MonthlyAlwaysPopulated()
    {
        await using var db = CreateContext();
        var accounts = new CashAccountService(db);
        await accounts.CreateAsync(new CreateCashAccountRequest("Bank", "Bank", null, 0m, new DateOnly(2026, 1, 1)));
        var forecast = new ForecastService(db, accounts);

        var shortHorizon = await forecast.ProjectAsync(60, "Expected");
        var longHorizon = await forecast.ProjectAsync(120, "Expected");

        Assert.Equal(61, shortHorizon.DailyPoints.Count);
        Assert.NotEmpty(shortHorizon.MonthlyPoints);
        Assert.Empty(longHorizon.DailyPoints);
        Assert.NotEmpty(longHorizon.MonthlyPoints);
    }

    [Fact]
    public async Task ProjectAsync_PaymentLag_FallsBackToBusinessWideMedian_WhenCustomerHasNoHistory()
    {
        await using var db = CreateContext();
        var accounts = new CashAccountService(db);
        await accounts.CreateAsync(new CreateCashAccountRequest("Bank", "Bank", null, 0m, new DateOnly(2026, 1, 1)));

        // Business-wide history from a different customer: lag of 7 days.
        var historyCustomer = Guid.NewGuid();
        await SeedInvoiceAsync(db, historyCustomer, new DateOnly(2026, 1, 1), 100m, isPaid: true, datePaid: new DateOnly(2026, 1, 8));

        // A brand-new customer with no payment history of their own.
        var newCustomer = Guid.NewGuid();
        await SeedInvoiceAsync(db, newCustomer, Today.AddDays(10), 500m);

        var forecast = new ForecastService(db, accounts);
        var result = await forecast.ProjectAsync(30, "Expected");

        var point = result.DailyPoints.Single(p => p.Date == Today.AddDays(10 + 7));
        Assert.Equal(500m, point.In);
    }
}
