using System.Text;
using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class CashTransactionServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static IFileStorage CreateStorage() =>
        new LocalFileStorage(Path.Combine(Path.GetTempPath(), "mobmek-tests", Guid.NewGuid().ToString("N")));

    // One bank account, one till, and one "Out"-only user category to write against.
    private static async Task<(CashTransactionService Service, CashAccountDto Bank, CashAccountDto Till, TransactionCategoryDto Parts)> SeedAsync(
        AppDbContext db, IFileStorage? storage = null)
    {
        var accounts = new CashAccountService(db);
        var bank = await accounts.CreateAsync(new CreateCashAccountRequest("Bank", "Bank", null, 1000m, new DateOnly(2026, 1, 1)));
        var till = await accounts.CreateAsync(new CreateCashAccountRequest("Till", "Cash", null, 100m, new DateOnly(2026, 1, 1)));
        var (parts, _) = await new TransactionCategoryService(db).CreateAsync(
            new CreateTransactionCategoryRequest("Parts & Materials", "Out", "Operating", null, false));

        return (new CashTransactionService(db, storage ?? CreateStorage(), new CashFlowAuditService(db)), bank, till, parts!);
    }

    private static CreateCashTransactionRequest NewOutflow(Guid accountId, Guid categoryId, decimal amount = 50m, string description = "Brake pads") =>
        new(accountId, "Out", amount, new DateOnly(2026, 6, 1), description, categoryId, null, "Repco", null, null, null);

    private static CashTransactionFilter Filter(
        Guid? accountId = null, Guid? categoryId = null, Guid? payeeId = null, string? direction = null,
        string? status = null, DateOnly? from = null, DateOnly? to = null, string? search = null,
        int page = 1, int pageSize = 50) =>
        new(accountId, categoryId, payeeId, direction, status, from, to, search, page, pageSize);

    private static Task SetLockDateAsync(AppDbContext db, DateOnly? lockDate) =>
        new CashFlowSettingsService(db, new CashFlowAuditService(db))
            .UpdateAsync(new UpdateCashFlowSettingsRequest(null, null, null, null, 0m, lockDate));

    [Fact]
    public async Task CreateAsync_DefaultsGstTreatmentFromCategory()
    {
        await using var db = CreateContext();
        var (service, bank, _, parts) = await SeedAsync(db);

        var (created, error) = await service.CreateAsync(NewOutflow(bank.Id, parts.Id));

        Assert.Equal(CashTransactionWriteError.None, error);
        Assert.Equal("Taxable", created!.GstTreatment);      // category default
        Assert.Equal("Cleared", created.Status);             // manual default
        Assert.Equal("Bank", created.AccountName);
        Assert.Equal("Parts & Materials", created.CategoryName);
    }

    [Fact]
    public async Task CreateAsync_ValidatesInputs()
    {
        await using var db = CreateContext();
        var (service, bank, _, parts) = await SeedAsync(db);

        var (_, badDirection) = await service.CreateAsync(NewOutflow(bank.Id, parts.Id) with { Direction = "Sideways" });
        var (_, badAmount) = await service.CreateAsync(NewOutflow(bank.Id, parts.Id, amount: 0m));
        var (_, badAccount) = await service.CreateAsync(NewOutflow(Guid.NewGuid(), parts.Id));
        var (_, badCategory) = await service.CreateAsync(NewOutflow(bank.Id, Guid.NewGuid()));
        var (_, mismatch) = await service.CreateAsync(NewOutflow(bank.Id, parts.Id) with { Direction = "In" });
        var (_, badGst) = await service.CreateAsync(NewOutflow(bank.Id, parts.Id) with { GstTreatment = "Sometimes" });
        var (_, badStatus) = await service.CreateAsync(NewOutflow(bank.Id, parts.Id) with { Status = "Reconciled" });
        var (_, badPayee) = await service.CreateAsync(NewOutflow(bank.Id, parts.Id) with { PayeeId = Guid.NewGuid() });

        Assert.Equal(CashTransactionWriteError.InvalidDirection, badDirection);
        Assert.Equal(CashTransactionWriteError.NonPositiveAmount, badAmount);
        Assert.Equal(CashTransactionWriteError.AccountNotFound, badAccount);
        Assert.Equal(CashTransactionWriteError.CategoryNotFound, badCategory);
        Assert.Equal(CashTransactionWriteError.DirectionMismatchesCategory, mismatch);
        Assert.Equal(CashTransactionWriteError.InvalidGstTreatment, badGst);
        Assert.Equal(CashTransactionWriteError.InvalidStatus, badStatus);   // reconciliation only
        Assert.Equal(CashTransactionWriteError.PayeeNotFound, badPayee);
    }

    [Fact]
    public async Task CreateAsync_LinkedPayee_SetsCounterpartyAndRefusesArchived()
    {
        await using var db = CreateContext();
        var (service, bank, _, parts) = await SeedAsync(db);
        var payees = new PayeeService(db, new CashFlowAuditService(db));
        var (repco, _) = await payees.CreateAsync(new CreatePayeeRequest("Repco Ltd", parts.Id, "Taxable", null));
        var (archived, _) = await payees.CreateAsync(new CreatePayeeRequest("Old Supplier", null, null, null));
        await payees.UpdateAsync(archived!.Id, new UpdatePayeeRequest("Old Supplier", null, null, null, IsArchived: true));

        var (created, ok) = await service.CreateAsync(
            NewOutflow(bank.Id, parts.Id) with { PayeeId = repco!.Id, Counterparty = "typed text" });
        var (_, archivedError) = await service.CreateAsync(NewOutflow(bank.Id, parts.Id) with { PayeeId = archived.Id });

        Assert.Equal(CashTransactionWriteError.None, ok);
        Assert.Equal(repco.Id, created!.PayeeId);
        Assert.Equal("Repco Ltd", created.Counterparty);    // payee name wins over typed text
        Assert.Equal(CashTransactionWriteError.PayeeArchived, archivedError);
    }

    [Fact]
    public async Task CreateAsync_RefusesArchivedAccount()
    {
        await using var db = CreateContext();
        var (service, bank, _, parts) = await SeedAsync(db);
        await new CashAccountService(db).UpdateAsync(bank.Id,
            new UpdateCashAccountRequest("Bank", "Bank", null, 1000m, new DateOnly(2026, 1, 1), IsArchived: true));

        var (_, error) = await service.CreateAsync(NewOutflow(bank.Id, parts.Id));

        Assert.Equal(CashTransactionWriteError.AccountArchived, error);
    }

    [Fact]
    public async Task PeriodLock_BlocksWritesOnOrBeforeLockDate()
    {
        await using var db = CreateContext();
        var (service, bank, till, parts) = await SeedAsync(db);
        var (beforeLock, _) = await service.CreateAsync(NewOutflow(bank.Id, parts.Id) with { Date = new DateOnly(2026, 5, 31) });
        var (afterLock, _) = await service.CreateAsync(NewOutflow(bank.Id, parts.Id) with { Date = new DateOnly(2026, 6, 2) });
        await SetLockDateAsync(db, new DateOnly(2026, 5, 31));

        var (_, createLocked) = await service.CreateAsync(NewOutflow(bank.Id, parts.Id) with { Date = new DateOnly(2026, 5, 31) });
        var (_, createOpen) = await service.CreateAsync(NewOutflow(bank.Id, parts.Id) with { Date = new DateOnly(2026, 6, 1) });
        var update = new UpdateCashTransactionRequest(bank.Id, "Out", 99m, new DateOnly(2026, 6, 7), "edited", parts.Id, null, null, null, null, null);
        var (_, updateLockedRow) = await service.UpdateAsync(beforeLock!.Id, update);
        var (_, moveIntoLocked) = await service.UpdateAsync(afterLock!.Id, update with { Date = new DateOnly(2026, 5, 1) });
        var deleteLocked = await service.DeleteAsync(beforeLock.Id);
        var (_, transferLocked) = await service.CreateTransferAsync(
            new CreateTransferRequest(bank.Id, till.Id, 10m, new DateOnly(2026, 5, 15), null, null));

        Assert.Equal(CashTransactionWriteError.PeriodLocked, createLocked);
        Assert.Equal(CashTransactionWriteError.None, createOpen);            // day after the lock is open
        Assert.Equal(CashTransactionWriteError.PeriodLocked, updateLockedRow);
        Assert.Equal(CashTransactionWriteError.PeriodLocked, moveIntoLocked);
        Assert.Equal(CashTransactionWriteError.PeriodLocked, deleteLocked);
        Assert.Equal(CashTransactionWriteError.PeriodLocked, transferLocked);
    }

    [Fact]
    public async Task ReconciledRows_AreImmutable()
    {
        await using var db = CreateContext();
        var (service, bank, _, parts) = await SeedAsync(db);
        var (created, _) = await service.CreateAsync(NewOutflow(bank.Id, parts.Id));
        var entity = await db.CashTransactions.SingleAsync(t => t.Id == created!.Id);
        entity.Status = "Reconciled";   // Phase 2's reconciliation will be the only writer of this
        await db.SaveChangesAsync();

        var update = new UpdateCashTransactionRequest(bank.Id, "Out", 99m, new DateOnly(2026, 6, 7), "edited", parts.Id, null, null, null, null, null);
        var (_, updateError) = await service.UpdateAsync(created!.Id, update);
        var deleteError = await service.DeleteAsync(created.Id);

        Assert.Equal(CashTransactionWriteError.ReconciledReadOnly, updateError);
        Assert.Equal(CashTransactionWriteError.ReconciledReadOnly, deleteError);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersAndTotals_ExcludeTransferLegs()
    {
        await using var db = CreateContext();
        var (service, bank, till, parts) = await SeedAsync(db);
        var (sales, _) = await new TransactionCategoryService(db).CreateAsync(
            new CreateTransactionCategoryRequest("Sales", "In", "Sales", null, false));

        await service.CreateAsync(new CreateCashTransactionRequest(bank.Id, "In", 500m, new DateOnly(2026, 6, 2), "Job payment", sales!.Id, null, null, null, null, null));
        await service.CreateAsync(NewOutflow(bank.Id, parts.Id, 200m));
        await service.CreateTransferAsync(new CreateTransferRequest(bank.Id, till.Id, 50m, new DateOnly(2026, 6, 3), null, null));

        var all = await service.GetPagedAsync(Filter());
        var bankOnly = await service.GetPagedAsync(Filter(accountId: bank.Id));
        var searched = await service.GetPagedAsync(Filter(search: "brake"));

        Assert.Equal(4, all.TotalCount);          // 2 manual + 2 transfer legs
        Assert.Equal(500m, all.TotalIn);          // transfer legs excluded from totals
        Assert.Equal(200m, all.TotalOut);
        Assert.Equal(3, bankOnly.TotalCount);
        Assert.Single(searched.Items);
        Assert.Equal("Brake pads", searched.Items[0].Description);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersByPayeeAndStatus()
    {
        await using var db = CreateContext();
        var (service, bank, _, parts) = await SeedAsync(db);
        var (repco, _) = await new PayeeService(db, new CashFlowAuditService(db))
            .CreateAsync(new CreatePayeeRequest("Repco Ltd", null, null, null));

        await service.CreateAsync(NewOutflow(bank.Id, parts.Id, 10m, "with payee") with { PayeeId = repco!.Id });
        await service.CreateAsync(NewOutflow(bank.Id, parts.Id, 20m, "pending one") with { Status = "Pending" });

        var byPayee = await service.GetPagedAsync(Filter(payeeId: repco.Id));
        var pending = await service.GetPagedAsync(Filter(status: "Pending"));

        Assert.Single(byPayee.Items);
        Assert.Equal("with payee", byPayee.Items[0].Description);
        Assert.Single(pending.Items);
        Assert.Equal("pending one", pending.Items[0].Description);
    }

    [Fact]
    public async Task GetPagedAsync_PagesNewestFirst()
    {
        await using var db = CreateContext();
        var (service, bank, _, parts) = await SeedAsync(db);
        await service.CreateAsync(NewOutflow(bank.Id, parts.Id, 10m, "older") with { Date = new DateOnly(2026, 5, 1) });
        await service.CreateAsync(NewOutflow(bank.Id, parts.Id, 20m, "newer") with { Date = new DateOnly(2026, 6, 1) });

        var page1 = await service.GetPagedAsync(Filter(page: 1, pageSize: 1));
        var page2 = await service.GetPagedAsync(Filter(page: 2, pageSize: 1));

        Assert.Equal("newer", page1.Items.Single().Description);
        Assert.Equal("older", page2.Items.Single().Description);
        Assert.Equal(2, page1.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_RunningBalance_OnlyForSingleAccountViews()
    {
        await using var db = CreateContext();
        var (service, bank, till, parts) = await SeedAsync(db);
        var (sales, _) = await new TransactionCategoryService(db).CreateAsync(
            new CreateTransactionCategoryRequest("Sales", "In", "Sales", null, false));

        // Opening 1000 → out 200 (6/1) = 800 → in 500 (6/2) = 1300 → transfer out 50 (6/3) = 1250.
        await service.CreateAsync(NewOutflow(bank.Id, parts.Id, 200m) with { Date = new DateOnly(2026, 6, 1) });
        await service.CreateAsync(new CreateCashTransactionRequest(bank.Id, "In", 500m, new DateOnly(2026, 6, 2), "Job payment", sales!.Id, null, null, null, null, null));
        await service.CreateTransferAsync(new CreateTransferRequest(bank.Id, till.Id, 50m, new DateOnly(2026, 6, 3), null, null));

        var bankView = await service.GetPagedAsync(Filter(accountId: bank.Id));
        var secondPage = await service.GetPagedAsync(Filter(accountId: bank.Id, page: 2, pageSize: 2));
        var allAccounts = await service.GetPagedAsync(Filter());
        var thinned = await service.GetPagedAsync(Filter(accountId: bank.Id, categoryId: parts.Id));

        Assert.Equal([1250m, 1300m, 800m], bankView.Items.Select(i => i.RunningBalance!.Value).ToList());
        Assert.Equal(800m, secondPage.Items.Single().RunningBalance);   // recomputed per page
        Assert.All(allAccounts.Items, i => Assert.Null(i.RunningBalance));
        Assert.All(thinned.Items, i => Assert.Null(i.RunningBalance));  // category filter thins rows out
    }

    [Fact]
    public async Task ExportCsvAsync_HonoursFilterAndEscapes()
    {
        await using var db = CreateContext();
        var (service, bank, _, parts) = await SeedAsync(db);
        await service.CreateAsync(NewOutflow(bank.Id, parts.Id, 12.5m, "Pads, \"heavy duty\""));
        await service.CreateAsync(NewOutflow(bank.Id, parts.Id, 99m, "Not exported") with { Date = new DateOnly(2026, 7, 1) });

        var csv = await service.ExportCsvAsync(Filter(to: new DateOnly(2026, 6, 30)));
        var lines = csv.TrimEnd().Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        Assert.Equal(2, lines.Count);   // header + one row
        Assert.StartsWith("Date,Account,Direction,Amount,Category", lines[0]);
        Assert.Contains("\"Pads, \"\"heavy duty\"\"\"", lines[1]);
        Assert.Contains("12.50", lines[1]);
        Assert.Contains("Manual", lines[1]);
        Assert.DoesNotContain("Not exported", csv);
    }

    [Fact]
    public async Task CreateTransferAsync_CreatesPairedLegs_UnderSystemCategory()
    {
        await using var db = CreateContext();
        var (service, bank, till, _) = await SeedAsync(db);

        var (legs, error) = await service.CreateTransferAsync(
            new CreateTransferRequest(bank.Id, till.Id, 100m, new DateOnly(2026, 6, 5), null, null));

        Assert.Equal(CashTransactionWriteError.None, error);
        Assert.Equal(2, legs!.Count);
        Assert.NotNull(legs[0].TransferGroupId);
        Assert.Equal(legs[0].TransferGroupId, legs[1].TransferGroupId);
        Assert.Contains(legs, l => l.AccountId == bank.Id && l.Direction == "Out" && l.Description == "Transfer to Till");
        Assert.Contains(legs, l => l.AccountId == till.Id && l.Direction == "In" && l.Description == "Transfer from Bank");
        Assert.All(legs, l => Assert.Equal(CashFlowSeeder.TransferCategory, l.CategoryName));

        // The pair nets to zero overall but moves both balances.
        var accounts = new CashAccountService(db);
        Assert.Equal(900m, (await accounts.GetByIdAsync(bank.Id))!.CurrentBalance);
        Assert.Equal(200m, (await accounts.GetByIdAsync(till.Id))!.CurrentBalance);
    }

    [Fact]
    public async Task CreateTransferAsync_ValidatesAccounts()
    {
        await using var db = CreateContext();
        var (service, bank, till, _) = await SeedAsync(db);

        var (_, same) = await service.CreateTransferAsync(new CreateTransferRequest(bank.Id, bank.Id, 10m, new DateOnly(2026, 6, 5), null, null));
        var (_, missing) = await service.CreateTransferAsync(new CreateTransferRequest(bank.Id, Guid.NewGuid(), 10m, new DateOnly(2026, 6, 5), null, null));
        var (_, zero) = await service.CreateTransferAsync(new CreateTransferRequest(bank.Id, till.Id, 0m, new DateOnly(2026, 6, 5), null, null));

        Assert.Equal(CashTransactionWriteError.SameAccountTransfer, same);
        Assert.Equal(CashTransactionWriteError.AccountNotFound, missing);
        Assert.Equal(CashTransactionWriteError.NonPositiveAmount, zero);
    }

    [Fact]
    public async Task CreateSplitAsync_CreatesSiblingRows_WithPerLineCategoryDefaults()
    {
        await using var db = CreateContext();
        var (service, bank, _, parts) = await SeedAsync(db);
        var (tools, _) = await new TransactionCategoryService(db).CreateAsync(
            new CreateTransactionCategoryRequest("Tools & Equipment", "Out", "Operating", "Exempt", false));

        var (lines, error) = await service.CreateSplitAsync(new CreateSplitTransactionRequest(
            bank.Id, "Out", new DateOnly(2026, 6, 10), "Trade store run", null, "Bunnings", null, null,
            [
                new SplitTransactionLine(80m, parts.Id, null, null),
                new SplitTransactionLine(120m, tools!.Id, null, "New drill"),
            ]));

        Assert.Equal(CashTransactionWriteError.None, error);
        Assert.Equal(2, lines!.Count);
        Assert.NotNull(lines[0].SplitGroupId);
        Assert.Equal(lines[0].SplitGroupId, lines[1].SplitGroupId);
        Assert.All(lines, l => Assert.Equal("Bunnings", l.Counterparty));
        Assert.Contains(lines, l => l.Amount == 80m && l.Description == "Trade store run" && l.GstTreatment == "Taxable");
        Assert.Contains(lines, l => l.Amount == 120m && l.Description == "New drill" && l.GstTreatment == "Exempt");

        // Each line is a normal ledger row, so totals just work.
        var totals = await service.GetPagedAsync(Filter());
        Assert.Equal(200m, totals.TotalOut);
    }

    [Fact]
    public async Task CreateSplitAsync_Validates()
    {
        await using var db = CreateContext();
        var (service, bank, _, parts) = await SeedAsync(db);

        var oneLine = new CreateSplitTransactionRequest(bank.Id, "Out", new DateOnly(2026, 6, 10), "x", null, null, null, null,
            [new SplitTransactionLine(80m, parts.Id, null, null)]);
        var zeroAmount = oneLine with { Lines = [new SplitTransactionLine(0m, parts.Id, null, null), new SplitTransactionLine(1m, parts.Id, null, null)] };
        var badCategory = oneLine with { Lines = [new SplitTransactionLine(1m, parts.Id, null, null), new SplitTransactionLine(1m, Guid.NewGuid(), null, null)] };

        Assert.Equal(CashTransactionWriteError.SplitNeedsTwoLines, (await service.CreateSplitAsync(oneLine)).Error);
        Assert.Equal(CashTransactionWriteError.NonPositiveAmount, (await service.CreateSplitAsync(zeroAmount)).Error);
        Assert.Equal(CashTransactionWriteError.CategoryNotFound, (await service.CreateSplitAsync(badCategory)).Error);
    }

    [Fact]
    public async Task SplitLines_EditAsGroup_DeleteRemovesWholeGroup()
    {
        await using var db = CreateContext();
        var (service, bank, _, parts) = await SeedAsync(db);
        var (lines, _) = await service.CreateSplitAsync(new CreateSplitTransactionRequest(
            bank.Id, "Out", new DateOnly(2026, 6, 10), "Trade store run", null, null, null, null,
            [new SplitTransactionLine(80m, parts.Id, null, null), new SplitTransactionLine(120m, parts.Id, null, null)]));
        var groupId = lines![0].SplitGroupId!.Value;

        // Individual edit refuses; the split endpoints manage the group.
        var update = new UpdateCashTransactionRequest(bank.Id, "Out", 99m, new DateOnly(2026, 6, 10), "edited", parts.Id, null, null, null, null, null);
        var (_, lineEdit) = await service.UpdateAsync(lines[0].Id, update);
        Assert.Equal(CashTransactionWriteError.SplitLineReadOnly, lineEdit);

        var (rewritten, rewriteError) = await service.UpdateSplitAsync(groupId, new UpdateSplitTransactionRequest(
            bank.Id, "Out", new DateOnly(2026, 6, 11), "Trade store run (fixed)", null, null, null, null,
            [new SplitTransactionLine(50m, parts.Id, null, null), new SplitTransactionLine(60m, parts.Id, null, null), new SplitTransactionLine(70m, parts.Id, null, null)]));

        Assert.Equal(CashTransactionWriteError.None, rewriteError);
        Assert.Equal(3, rewritten!.Count);
        Assert.All(rewritten, l => Assert.Equal(groupId, l.SplitGroupId));
        Assert.Equal(3, await db.CashTransactions.CountAsync(t => t.SplitGroupId == groupId));

        // Deleting one line undoes the whole entry.
        Assert.Equal(CashTransactionWriteError.None, await service.DeleteAsync(rewritten[0].Id));
        Assert.Equal(0, await db.CashTransactions.CountAsync(t => t.SplitGroupId == groupId));
    }

    [Fact]
    public async Task UpdateSplitAsync_ReturnsNotFound_ForUnknownGroup()
    {
        await using var db = CreateContext();
        var (service, bank, _, parts) = await SeedAsync(db);

        var (_, error) = await service.UpdateSplitAsync(Guid.NewGuid(), new UpdateSplitTransactionRequest(
            bank.Id, "Out", new DateOnly(2026, 6, 10), "x", null, null, null, null,
            [new SplitTransactionLine(1m, parts.Id, null, null), new SplitTransactionLine(2m, parts.Id, null, null)]));

        Assert.Equal(CashTransactionWriteError.NotFound, error);
    }

    [Fact]
    public async Task BulkAsync_SetCategory_SkipsProtectedRowsWithReasons()
    {
        await using var db = CreateContext();
        var (service, bank, till, parts) = await SeedAsync(db);
        var (fuel, _) = await new TransactionCategoryService(db).CreateAsync(
            new CreateTransactionCategoryRequest("Vehicle & Fuel", "Out", "Operating", null, false));

        var (plain, _) = await service.CreateAsync(NewOutflow(bank.Id, parts.Id, 10m, "recategorize me"));
        var (legs, _) = await service.CreateTransferAsync(new CreateTransferRequest(bank.Id, till.Id, 10m, new DateOnly(2026, 6, 5), null, null));
        var invoicePosted = new CashTransaction
        {
            AccountId = bank.Id, Direction = "In", Amount = 100m, Date = new DateOnly(2026, 6, 6),
            Description = "Invoice INV-0001", CategoryId = parts.Id, InvoiceId = Guid.NewGuid(),
        };
        db.CashTransactions.Add(invoicePosted);
        await db.SaveChangesAsync();
        var missingId = Guid.NewGuid();

        var (result, error) = await service.BulkAsync(new BulkCashTransactionRequest(
            [plain!.Id, legs![0].Id, invoicePosted.Id, missingId], "SetCategory", fuel!.Id, null));

        Assert.Equal(CashTransactionWriteError.None, error);
        Assert.Equal(1, result!.UpdatedCount);
        Assert.Equal(3, result.Skipped.Count);
        Assert.Contains(result.Skipped, s => s.Id == missingId && s.Reason == "Not found");
        Assert.Contains(result.Skipped, s => s.Id == legs[0].Id && s.Reason.Contains("Transfer"));
        Assert.Contains(result.Skipped, s => s.Id == invoicePosted.Id && s.Reason.Contains("invoice"));
        Assert.Equal(fuel.Id, (await service.GetByIdAsync(plain.Id))!.CategoryId);
    }

    [Fact]
    public async Task BulkAsync_SetStatus_AllowsManagedRows_ButNeverReconciled()
    {
        await using var db = CreateContext();
        var (service, bank, till, parts) = await SeedAsync(db);
        var (plain, _) = await service.CreateAsync(NewOutflow(bank.Id, parts.Id, 10m));
        var (legs, _) = await service.CreateTransferAsync(new CreateTransferRequest(bank.Id, till.Id, 10m, new DateOnly(2026, 6, 5), null, null));
        var (reconciled, _) = await service.CreateAsync(NewOutflow(bank.Id, parts.Id, 20m, "already reconciled"));
        var entity = await db.CashTransactions.SingleAsync(t => t.Id == reconciled!.Id);
        entity.Status = "Reconciled";
        await db.SaveChangesAsync();

        var (result, _) = await service.BulkAsync(new BulkCashTransactionRequest(
            [plain!.Id, legs![0].Id, reconciled!.Id], "SetStatus", null, "Pending"));

        Assert.Equal(2, result!.UpdatedCount);   // manual row + transfer leg both allowed
        Assert.Single(result.Skipped);
        Assert.Contains("immutable", result.Skipped[0].Reason);
        Assert.Equal("Pending", (await service.GetByIdAsync(legs[0].Id))!.Status);
    }

    [Fact]
    public async Task BulkAsync_Delete_And_InvalidAction()
    {
        await using var db = CreateContext();
        var (service, bank, _, parts) = await SeedAsync(db);
        var (a, _) = await service.CreateAsync(NewOutflow(bank.Id, parts.Id, 10m, "a"));
        var (b, _) = await service.CreateAsync(NewOutflow(bank.Id, parts.Id, 20m, "b"));
        var (splitLines, _) = await service.CreateSplitAsync(new CreateSplitTransactionRequest(
            bank.Id, "Out", new DateOnly(2026, 6, 10), "split", null, null, null, null,
            [new SplitTransactionLine(1m, parts.Id, null, null), new SplitTransactionLine(2m, parts.Id, null, null)]));

        var (result, error) = await service.BulkAsync(new BulkCashTransactionRequest(
            [a!.Id, b!.Id, splitLines![0].Id], "Delete", null, null));
        var (_, invalidAction) = await service.BulkAsync(new BulkCashTransactionRequest([a.Id], "Explode", null, null));
        var (_, invalidStatus) = await service.BulkAsync(new BulkCashTransactionRequest([a.Id], "SetStatus", null, "Reconciled"));
        var (_, badCategory) = await service.BulkAsync(new BulkCashTransactionRequest([a.Id], "SetCategory", Guid.NewGuid(), null));

        Assert.Equal(CashTransactionWriteError.None, error);
        Assert.Equal(2, result!.UpdatedCount);
        Assert.Single(result.Skipped);           // split line must be deleted via its group
        Assert.Contains("split", result.Skipped[0].Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Null(await service.GetByIdAsync(a.Id));
        Assert.Equal(CashTransactionWriteError.InvalidBulkAction, invalidAction);
        Assert.Equal(CashTransactionWriteError.InvalidStatus, invalidStatus);
        Assert.Equal(CashTransactionWriteError.CategoryNotFound, badCategory);
    }

    [Fact]
    public async Task Mutations_WriteAuditTrail()
    {
        await using var db = CreateContext();
        var (service, bank, _, parts) = await SeedAsync(db);

        var (created, _) = await service.CreateAsync(NewOutflow(bank.Id, parts.Id, 120m));
        await service.UpdateAsync(created!.Id, new UpdateCashTransactionRequest(
            bank.Id, "Out", 150m, created.Date, created.Description, parts.Id, null, "Repco", null, null, null));
        await service.DeleteAsync(created.Id);

        var auditService = new CashFlowAuditService(db);
        var trail = await auditService.GetPagedAsync(new CashFlowAuditFilter("CashTransaction", created.Id, null, null));

        Assert.Equal(3, trail.TotalCount);
        Assert.Equal(["Created", "Deleted", "Updated"], trail.Items.Select(i => i.Action).OrderBy(a => a).ToList());
        var update = trail.Items.Single(i => i.Action == "Updated");
        Assert.NotNull(update.Changes);
        var amountChange = update.Changes!.Single(c => c.Field == "Amount");
        Assert.Equal("120.00", amountChange.Old);
        Assert.Equal("150.00", amountChange.New);
    }

    [Fact]
    public async Task UpdateAsync_RefusesManagedRows()
    {
        await using var db = CreateContext();
        var (service, bank, till, parts) = await SeedAsync(db);
        var (legs, _) = await service.CreateTransferAsync(new CreateTransferRequest(bank.Id, till.Id, 10m, new DateOnly(2026, 6, 5), null, null));

        var invoicePosted = new CashTransaction
        {
            AccountId = bank.Id, Direction = "In", Amount = 100m, Date = new DateOnly(2026, 6, 6),
            Description = "Invoice INV-0001", CategoryId = parts.Id, InvoiceId = Guid.NewGuid(),
        };
        db.CashTransactions.Add(invoicePosted);
        await db.SaveChangesAsync();

        var update = new UpdateCashTransactionRequest(bank.Id, "Out", 99m, new DateOnly(2026, 6, 7), "edited", parts.Id, null, null, null, null, null);
        var (_, transferError) = await service.UpdateAsync(legs![0].Id, update);
        var (_, invoiceError) = await service.UpdateAsync(invoicePosted.Id, update);
        var (_, notFound) = await service.UpdateAsync(Guid.NewGuid(), update);

        Assert.Equal(CashTransactionWriteError.TransferLegReadOnly, transferError);
        Assert.Equal(CashTransactionWriteError.InvoiceLinkedReadOnly, invoiceError);
        Assert.Equal(CashTransactionWriteError.NotFound, notFound);
    }

    [Fact]
    public async Task DeleteAsync_RemovesBothTransferLegs_ButRefusesInvoicePostedRows()
    {
        await using var db = CreateContext();
        var (service, bank, till, parts) = await SeedAsync(db);
        var (legs, _) = await service.CreateTransferAsync(new CreateTransferRequest(bank.Id, till.Id, 10m, new DateOnly(2026, 6, 5), null, null));

        var invoicePosted = new CashTransaction
        {
            AccountId = bank.Id, Direction = "In", Amount = 100m, Date = new DateOnly(2026, 6, 6),
            Description = "Invoice INV-0001", CategoryId = parts.Id, InvoiceId = Guid.NewGuid(),
        };
        db.CashTransactions.Add(invoicePosted);
        await db.SaveChangesAsync();

        Assert.Equal(CashTransactionWriteError.None, await service.DeleteAsync(legs![0].Id));
        Assert.Null(await service.GetByIdAsync(legs[1].Id));    // the other leg went too

        Assert.Equal(CashTransactionWriteError.InvoiceLinkedReadOnly, await service.DeleteAsync(invoicePosted.Id));
    }

    [Fact]
    public async Task Attachments_UploadDownloadDelete_RoundTrip()
    {
        await using var db = CreateContext();
        var storage = CreateStorage();
        var (service, bank, _, parts) = await SeedAsync(db, storage);
        var (transaction, _) = await service.CreateAsync(NewOutflow(bank.Id, parts.Id));

        var bytes = Encoding.UTF8.GetBytes("receipt content");
        var attachment = await service.AddAttachmentAsync(
            transaction!.Id, new MemoryStream(bytes), "receipt.pdf", "application/pdf", bytes.Length);

        Assert.NotNull(attachment);
        Assert.Equal("receipt.pdf", attachment!.FileName);
        Assert.Equal(bytes.Length, attachment.SizeBytes);

        var download = await service.GetAttachmentAsync(transaction.Id, attachment.Id);
        Assert.NotNull(download);
        using var reader = new StreamReader(download!.Value.Content);
        Assert.Equal("receipt content", await reader.ReadToEndAsync());

        Assert.True(await service.DeleteAttachmentAsync(transaction.Id, attachment.Id));
        Assert.Null(await service.GetAttachmentAsync(transaction.Id, attachment.Id));
    }

    [Fact]
    public async Task AddAttachmentAsync_ReturnsNull_WhenTransactionMissing()
    {
        await using var db = CreateContext();
        var (service, _, _, _) = await SeedAsync(db);

        var attachment = await service.AddAttachmentAsync(
            Guid.NewGuid(), new MemoryStream([1]), "x.png", "image/png", 1);

        Assert.Null(attachment);
    }

    [Fact]
    public async Task DeleteAsync_RemovesStoredAttachmentFiles()
    {
        await using var db = CreateContext();
        var storage = CreateStorage();
        var (service, bank, _, parts) = await SeedAsync(db, storage);
        var (transaction, _) = await service.CreateAsync(NewOutflow(bank.Id, parts.Id));
        var attachment = await service.AddAttachmentAsync(
            transaction!.Id, new MemoryStream([1, 2, 3]), "r.jpg", "image/jpeg", 3);
        var storageKey = (await db.TransactionAttachments.SingleAsync(a => a.Id == attachment!.Id)).StorageKey;

        await service.DeleteAsync(transaction.Id);

        Assert.Null(await storage.OpenReadAsync(storageKey));
    }
}
