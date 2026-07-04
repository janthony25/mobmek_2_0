using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class GstReportServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    // One bank account and one cash till, plus a "Sales" category, to post transactions against.
    private static async Task<(Guid BankId, Guid TillId, Guid CategoryId)> SeedAccountsAsync(AppDbContext db)
    {
        var accounts = new CashAccountService(db);
        var bank = await accounts.CreateAsync(new CreateCashAccountRequest("Bank", "Bank", null, 0m, new DateOnly(2026, 1, 1)));
        var till = await accounts.CreateAsync(new CreateCashAccountRequest("Till", "Cash", null, 0m, new DateOnly(2026, 1, 1)));
        var (category, _) = await new TransactionCategoryService(db).CreateAsync(
            new CreateTransactionCategoryRequest("Sales", "In", "Operating", null, false));

        return (bank.Id, till.Id, category!.Id);
    }

    private static async Task AddTransactionAsync(
        AppDbContext db, Guid accountId, Guid categoryId, string direction, decimal amount, DateOnly date,
        string gstTreatment = "Taxable", Guid? transferGroupId = null)
    {
        db.CashTransactions.Add(new CashTransaction
        {
            AccountId = accountId,
            Direction = direction,
            Amount = amount,
            Date = date,
            Description = "Test",
            CategoryId = categoryId,
            GstTreatment = gstTreatment,
            TransferGroupId = transferGroupId,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetReportAsync_SplitsCashFromNonCashAccounts()
    {
        await using var db = CreateContext();
        var (bankId, tillId, categoryId) = await SeedAccountsAsync(db);
        var range = (new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));

        // $115 in via bank => $15 GST; $230 in via cash till => $30 GST.
        await AddTransactionAsync(db, bankId, categoryId, "In", 115m, new DateOnly(2026, 6, 10));
        await AddTransactionAsync(db, tillId, categoryId, "In", 230m, new DateOnly(2026, 6, 15));

        var report = await new GstReportService(db, new GstSettingService(db)).GetReportAsync(range.Item1, range.Item2);

        Assert.Equal(45m, report.Included.GstOnSales);
        Assert.Equal(15m, report.Excluded.GstOnSales);
        Assert.Equal(30m, report.CashGst);
    }

    [Fact]
    public async Task GetReportAsync_NetsSalesAgainstPurchases()
    {
        await using var db = CreateContext();
        var (bankId, _, categoryId) = await SeedAccountsAsync(db);

        await AddTransactionAsync(db, bankId, categoryId, "In", 115m, new DateOnly(2026, 6, 10));
        await AddTransactionAsync(db, bankId, categoryId, "Out", 57.50m, new DateOnly(2026, 6, 12));

        var report = await new GstReportService(db, new GstSettingService(db))
            .GetReportAsync(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));

        Assert.Equal(15m, report.Included.GstOnSales);
        Assert.Equal(7.5m, report.Included.GstOnPurchases);
        Assert.Equal(7.5m, report.Included.NetGst);
    }

    [Fact]
    public async Task GetReportAsync_IgnoresExemptTransactionsAndTransferLegs()
    {
        await using var db = CreateContext();
        var (bankId, _, categoryId) = await SeedAccountsAsync(db);
        var transferGroupId = Guid.NewGuid();

        await AddTransactionAsync(db, bankId, categoryId, "In", 115m, new DateOnly(2026, 6, 10), gstTreatment: "Exempt");
        await AddTransactionAsync(db, bankId, categoryId, "In", 500m, new DateOnly(2026, 6, 11), transferGroupId: transferGroupId);

        var report = await new GstReportService(db, new GstSettingService(db))
            .GetReportAsync(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));

        Assert.Equal(0m, report.Included.GstOnSales);
        Assert.Equal(0m, report.Included.NetGst);
    }

    [Fact]
    public async Task GetReportAsync_FiltersOutsideDateRange()
    {
        await using var db = CreateContext();
        var (bankId, _, categoryId) = await SeedAccountsAsync(db);

        await AddTransactionAsync(db, bankId, categoryId, "In", 115m, new DateOnly(2026, 5, 31));
        await AddTransactionAsync(db, bankId, categoryId, "In", 115m, new DateOnly(2026, 7, 1));

        var report = await new GstReportService(db, new GstSettingService(db))
            .GetReportAsync(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));

        Assert.Equal(0m, report.Included.GstOnSales);
    }
}
