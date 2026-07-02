using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class CashFlowSettingsServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task GetCurrentAsync_CreatesEmptySingletonOnFirstUse()
    {
        await using var db = CreateContext();
        var service = new CashFlowSettingsService(db);

        var first = await service.GetCurrentAsync();
        var second = await service.GetCurrentAsync();

        Assert.Equal(first.Id, second.Id);
        Assert.Null(first.DefaultAccountId);
        Assert.Equal(1, await db.CashFlowSettings.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_StoresRouting()
    {
        await using var db = CreateContext();
        var accounts = new CashAccountService(db);
        var bank = await accounts.CreateAsync(new CreateCashAccountRequest("Bank", "Bank", null, 0m, new DateOnly(2026, 1, 1)));
        var till = await accounts.CreateAsync(new CreateCashAccountRequest("Till", "Cash", null, 0m, new DateOnly(2026, 1, 1)));
        var service = new CashFlowSettingsService(db);

        var updated = await service.UpdateAsync(new UpdateCashFlowSettingsRequest(bank.Id, till.Id, bank.Id, bank.Id));

        Assert.NotNull(updated);
        Assert.Equal(bank.Id, updated!.DefaultAccountId);
        Assert.Equal(till.Id, updated.CashAccountId);
        Assert.Equal(bank.Id, updated.CardAccountId);
        Assert.Equal(bank.Id, updated.BankTransferAccountId);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenAnyAccountIdUnknown()
    {
        await using var db = CreateContext();
        var service = new CashFlowSettingsService(db);

        var updated = await service.UpdateAsync(new UpdateCashFlowSettingsRequest(Guid.NewGuid(), null, null, null));

        Assert.Null(updated);
        Assert.Null((await service.GetCurrentAsync()).DefaultAccountId);   // nothing was applied
    }
}
