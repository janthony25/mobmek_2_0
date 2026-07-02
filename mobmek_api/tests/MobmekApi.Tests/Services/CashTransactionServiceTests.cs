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

        return (new CashTransactionService(db, storage ?? CreateStorage()), bank, till, parts!);
    }

    private static CreateCashTransactionRequest NewOutflow(Guid accountId, Guid categoryId, decimal amount = 50m, string description = "Brake pads") =>
        new(accountId, "Out", amount, new DateOnly(2026, 6, 1), description, categoryId, "Repco", null, null);

    [Fact]
    public async Task CreateAsync_DefaultsGstTreatmentFromCategory()
    {
        await using var db = CreateContext();
        var (service, bank, _, parts) = await SeedAsync(db);

        var (created, error) = await service.CreateAsync(NewOutflow(bank.Id, parts.Id));

        Assert.Equal(CashTransactionWriteError.None, error);
        Assert.Equal("Taxable", created!.GstTreatment);      // category default
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

        Assert.Equal(CashTransactionWriteError.InvalidDirection, badDirection);
        Assert.Equal(CashTransactionWriteError.NonPositiveAmount, badAmount);
        Assert.Equal(CashTransactionWriteError.AccountNotFound, badAccount);
        Assert.Equal(CashTransactionWriteError.CategoryNotFound, badCategory);
        Assert.Equal(CashTransactionWriteError.DirectionMismatchesCategory, mismatch);
        Assert.Equal(CashTransactionWriteError.InvalidGstTreatment, badGst);
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
    public async Task GetPagedAsync_FiltersAndTotals_ExcludeTransferLegs()
    {
        await using var db = CreateContext();
        var (service, bank, till, parts) = await SeedAsync(db);
        var (sales, _) = await new TransactionCategoryService(db).CreateAsync(
            new CreateTransactionCategoryRequest("Sales", "In", "Sales", null, false));

        await service.CreateAsync(new CreateCashTransactionRequest(bank.Id, "In", 500m, new DateOnly(2026, 6, 2), "Job payment", sales!.Id, null, null, null));
        await service.CreateAsync(NewOutflow(bank.Id, parts.Id, 200m));
        await service.CreateTransferAsync(new CreateTransferRequest(bank.Id, till.Id, 50m, new DateOnly(2026, 6, 3), null, null));

        var all = await service.GetPagedAsync(new CashTransactionFilter(null, null, null, null, null, null));
        var bankOnly = await service.GetPagedAsync(new CashTransactionFilter(bank.Id, null, null, null, null, null));
        var searched = await service.GetPagedAsync(new CashTransactionFilter(null, null, null, null, null, "brake"));

        Assert.Equal(4, all.TotalCount);          // 2 manual + 2 transfer legs
        Assert.Equal(500m, all.TotalIn);          // transfer legs excluded from totals
        Assert.Equal(200m, all.TotalOut);
        Assert.Equal(3, bankOnly.TotalCount);
        Assert.Single(searched.Items);
        Assert.Equal("Brake pads", searched.Items[0].Description);
    }

    [Fact]
    public async Task GetPagedAsync_PagesNewestFirst()
    {
        await using var db = CreateContext();
        var (service, bank, _, parts) = await SeedAsync(db);
        await service.CreateAsync(NewOutflow(bank.Id, parts.Id, 10m, "older") with { Date = new DateOnly(2026, 5, 1) });
        await service.CreateAsync(NewOutflow(bank.Id, parts.Id, 20m, "newer") with { Date = new DateOnly(2026, 6, 1) });

        var page1 = await service.GetPagedAsync(new CashTransactionFilter(null, null, null, null, null, null, Page: 1, PageSize: 1));
        var page2 = await service.GetPagedAsync(new CashTransactionFilter(null, null, null, null, null, null, Page: 2, PageSize: 1));

        Assert.Equal("newer", page1.Items.Single().Description);
        Assert.Equal("older", page2.Items.Single().Description);
        Assert.Equal(2, page1.TotalCount);
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

        var update = new UpdateCashTransactionRequest(bank.Id, "Out", 99m, new DateOnly(2026, 6, 7), "edited", parts.Id, null, null, null);
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
