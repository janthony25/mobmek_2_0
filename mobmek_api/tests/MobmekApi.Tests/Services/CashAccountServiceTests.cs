using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class CashAccountServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static CreateCashAccountRequest NewAccount(string name = "ANZ Business", decimal opening = 1000m) =>
        new(name, "Bank", "01-1234-5678", opening, new DateOnly(2026, 1, 1));

    [Fact]
    public async Task CreateAsync_StartsBalanceAtOpeningBalance()
    {
        await using var db = CreateContext();
        var service = new CashAccountService(db);

        var account = await service.CreateAsync(NewAccount(opening: 1500m));

        Assert.Equal("ANZ Business", account.Name);
        Assert.Equal(1500m, account.OpeningBalance);
        Assert.Equal(1500m, account.CurrentBalance);
        Assert.False(account.IsArchived);
    }

    [Fact]
    public async Task GetByIdAsync_DerivesBalanceFromTransactions()
    {
        await using var db = CreateContext();
        var service = new CashAccountService(db);
        var account = await service.CreateAsync(NewAccount(opening: 1000m));

        var category = new TransactionCategory { Name = "Misc" };
        db.TransactionCategories.Add(category);
        db.CashTransactions.Add(new CashTransaction
        {
            AccountId = account.Id, Direction = "In", Amount = 250m,
            Date = new DateOnly(2026, 2, 1), Description = "Sale", CategoryId = category.Id,
        });
        db.CashTransactions.Add(new CashTransaction
        {
            AccountId = account.Id, Direction = "Out", Amount = 100m,
            Date = new DateOnly(2026, 2, 2), Description = "Parts", CategoryId = category.Id,
        });
        await db.SaveChangesAsync();

        var fetched = await service.GetByIdAsync(account.Id);

        Assert.Equal(1150m, fetched!.CurrentBalance);   // 1000 + 250 - 100
    }

    [Fact]
    public async Task GetAllAsync_HidesArchived_UnlessAsked()
    {
        await using var db = CreateContext();
        var service = new CashAccountService(db);
        await service.CreateAsync(NewAccount("Active"));
        var archived = await service.CreateAsync(NewAccount("Old till"));
        await service.UpdateAsync(archived.Id, new UpdateCashAccountRequest("Old till", "Cash", null, 0m, new DateOnly(2026, 1, 1), IsArchived: true));

        var visible = await service.GetAllAsync();
        var all = await service.GetAllAsync(includeArchived: true);

        Assert.Single(visible);
        Assert.Equal("Active", visible[0].Name);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenMissing()
    {
        await using var db = CreateContext();
        var service = new CashAccountService(db);

        var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateCashAccountRequest("X", "Bank", null, 0m, new DateOnly(2026, 1, 1), false));

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_StampsUpdatedAtUtc()
    {
        await using var db = CreateContext();
        var service = new CashAccountService(db);
        var account = await service.CreateAsync(NewAccount());

        var updated = await service.UpdateAsync(account.Id, new UpdateCashAccountRequest("Renamed", "Bank", null, 1000m, new DateOnly(2026, 1, 1), false));

        Assert.Equal("Renamed", updated!.Name);
        Assert.NotNull(updated.UpdatedAtUtc);
    }

    [Fact]
    public async Task DeleteAsync_RefusesWhenAccountHasTransactions()
    {
        await using var db = CreateContext();
        var service = new CashAccountService(db);
        var account = await service.CreateAsync(NewAccount());

        var category = new TransactionCategory { Name = "Misc" };
        db.TransactionCategories.Add(category);
        db.CashTransactions.Add(new CashTransaction
        {
            AccountId = account.Id, Direction = "In", Amount = 10m,
            Date = new DateOnly(2026, 2, 1), Description = "Sale", CategoryId = category.Id,
        });
        await db.SaveChangesAsync();

        var result = await service.DeleteAsync(account.Id);

        Assert.Equal(CashAccountDeleteResult.HasTransactions, result);
        Assert.NotNull(await service.GetByIdAsync(account.Id));
    }

    [Fact]
    public async Task DeleteAsync_ClearsInvoiceRoutingReferences()
    {
        await using var db = CreateContext();
        var service = new CashAccountService(db);
        var account = await service.CreateAsync(NewAccount());
        var settingsService = new CashFlowSettingsService(db, new CashFlowAuditService(db));
        await settingsService.UpdateAsync(new UpdateCashFlowSettingsRequest(account.Id, account.Id, null, null, 0m, null));

        var result = await service.DeleteAsync(account.Id);

        Assert.Equal(CashAccountDeleteResult.Deleted, result);
        var settings = await settingsService.GetCurrentAsync();
        Assert.Null(settings.DefaultAccountId);
        Assert.Null(settings.CashAccountId);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNotFound_WhenMissing()
    {
        await using var db = CreateContext();
        var service = new CashAccountService(db);

        Assert.Equal(CashAccountDeleteResult.NotFound, await service.DeleteAsync(Guid.NewGuid()));
    }
}
