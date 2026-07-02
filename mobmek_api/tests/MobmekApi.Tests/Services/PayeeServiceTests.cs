using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class PayeeServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static PayeeService CreateService(AppDbContext db) => new(db, new CashFlowAuditService(db));

    private static async Task<(CashAccountDto Bank, TransactionCategoryDto Parts)> SeedLedgerAsync(AppDbContext db)
    {
        var bank = await new CashAccountService(db).CreateAsync(
            new CreateCashAccountRequest("Bank", "Bank", null, 1000m, new DateOnly(2026, 1, 1)));
        var (parts, _) = await new TransactionCategoryService(db).CreateAsync(
            new CreateTransactionCategoryRequest("Parts & Materials", "Out", "Operating", null, false));
        return (bank, parts!);
    }

    [Fact]
    public async Task CreateAsync_TrimsName_AndReturnsDefaults()
    {
        await using var db = CreateContext();
        var (_, parts) = await SeedLedgerAsync(db);
        var service = CreateService(db);

        var (created, error) = await service.CreateAsync(new CreatePayeeRequest("  Repco Ltd  ", parts.Id, "Taxable", "trade account"));

        Assert.Equal(PayeeWriteError.None, error);
        Assert.Equal("Repco Ltd", created!.Name);
        Assert.Equal(parts.Id, created.DefaultCategoryId);
        Assert.Equal("Parts & Materials", created.DefaultCategoryName);
        Assert.Equal("Taxable", created.DefaultGstTreatment);
    }

    [Fact]
    public async Task CreateAsync_Validates()
    {
        await using var db = CreateContext();
        var service = CreateService(db);
        await service.CreateAsync(new CreatePayeeRequest("Repco Ltd", null, null, null));

        var (_, duplicate) = await service.CreateAsync(new CreatePayeeRequest("repco ltd", null, null, null));
        var (_, badCategory) = await service.CreateAsync(new CreatePayeeRequest("Other", Guid.NewGuid(), null, null));
        var (_, badGst) = await service.CreateAsync(new CreatePayeeRequest("Other", null, "Sometimes", null));

        Assert.Equal(PayeeWriteError.DuplicateName, duplicate);        // case-insensitive
        Assert.Equal(PayeeWriteError.CategoryNotFound, badCategory);
        Assert.Equal(PayeeWriteError.InvalidGstTreatment, badGst);
    }

    [Fact]
    public async Task GetAllAsync_HidesArchived_UnlessAsked()
    {
        await using var db = CreateContext();
        var service = CreateService(db);
        await service.CreateAsync(new CreatePayeeRequest("Active", null, null, null));
        var (archived, _) = await service.CreateAsync(new CreatePayeeRequest("Archived", null, null, null));
        await service.UpdateAsync(archived!.Id, new UpdatePayeeRequest("Archived", null, null, null, IsArchived: true));

        Assert.Single(await service.GetAllAsync());
        Assert.Equal(2, (await service.GetAllAsync(includeArchived: true)).Count);
    }

    [Fact]
    public async Task UpdateAsync_RenameKeepsExistingCounterpartyText()
    {
        await using var db = CreateContext();
        var (bank, parts) = await SeedLedgerAsync(db);
        var service = CreateService(db);
        var (payee, _) = await service.CreateAsync(new CreatePayeeRequest("Repco", null, null, null));
        var transactions = new CashTransactionService(db, new LocalFileStorage(Path.GetTempPath()), new CashFlowAuditService(db));
        var (posted, _) = await transactions.CreateAsync(new CreateCashTransactionRequest(
            bank.Id, "Out", 10m, new DateOnly(2026, 6, 1), "pads", parts.Id, payee!.Id, null, null, null, null));

        var (renamed, error) = await service.UpdateAsync(payee.Id, new UpdatePayeeRequest("Repco NZ", null, null, null, false));

        Assert.Equal(PayeeWriteError.None, error);
        Assert.Equal("Repco NZ", renamed!.Name);
        // History keeps the display text it was posted with — renames don't rewrite the past.
        Assert.Equal("Repco", (await transactions.GetByIdAsync(posted!.Id))!.Counterparty);
    }

    [Fact]
    public async Task DeleteAsync_RefusesWhileReferenced()
    {
        await using var db = CreateContext();
        var (bank, parts) = await SeedLedgerAsync(db);
        var service = CreateService(db);
        var (used, _) = await service.CreateAsync(new CreatePayeeRequest("Used", null, null, null));
        var (unused, _) = await service.CreateAsync(new CreatePayeeRequest("Unused", null, null, null));
        await new CashTransactionService(db, new LocalFileStorage(Path.GetTempPath()), new CashFlowAuditService(db))
            .CreateAsync(new CreateCashTransactionRequest(
                bank.Id, "Out", 10m, new DateOnly(2026, 6, 1), "pads", parts.Id, used!.Id, null, null, null, null));

        Assert.Equal(PayeeWriteError.InUse, await service.DeleteAsync(used.Id));
        Assert.Equal(PayeeWriteError.None, await service.DeleteAsync(unused!.Id));
        Assert.Equal(PayeeWriteError.NotFound, await service.DeleteAsync(unused.Id));
    }

    [Fact]
    public async Task GetSummaryAsync_Reports12MonthTotalsAndDates()
    {
        await using var db = CreateContext();
        var (bank, parts) = await SeedLedgerAsync(db);
        var (sales, _) = await new TransactionCategoryService(db).CreateAsync(
            new CreateTransactionCategoryRequest("Sales", "In", "Sales", null, false));
        var service = CreateService(db);
        var (payee, _) = await service.CreateAsync(new CreatePayeeRequest("Repco", null, null, null));
        var transactions = new CashTransactionService(db, new LocalFileStorage(Path.GetTempPath()), new CashFlowAuditService(db));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await transactions.CreateAsync(new CreateCashTransactionRequest(
            bank.Id, "Out", 100m, today.AddMonths(-1), "recent out", parts.Id, payee!.Id, null, null, null, null));
        await transactions.CreateAsync(new CreateCashTransactionRequest(
            bank.Id, "In", 40m, today.AddMonths(-2), "recent in", sales!.Id, payee.Id, null, null, null, null));
        await transactions.CreateAsync(new CreateCashTransactionRequest(
            bank.Id, "Out", 999m, today.AddMonths(-18), "old out", parts.Id, payee.Id, null, null, null, null));

        var summary = await service.GetSummaryAsync(payee.Id);

        Assert.NotNull(summary);
        Assert.Equal(3, summary!.TransactionCount);
        Assert.Equal(today.AddMonths(-18), summary.FirstDate);
        Assert.Equal(today.AddMonths(-1), summary.LastDate);
        Assert.Equal(40m, summary.TotalIn12Months);
        Assert.Equal(100m, summary.TotalOut12Months);    // the 18-month-old row is outside the window

        Assert.Null(await service.GetSummaryAsync(Guid.NewGuid()));
    }
}
