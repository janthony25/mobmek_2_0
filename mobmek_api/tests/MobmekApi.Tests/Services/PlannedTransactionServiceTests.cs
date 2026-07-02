using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class PlannedTransactionServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static async Task<(PlannedTransactionService Service, CashAccountDto Bank, TransactionCategoryDto Tools)> SeedAsync(AppDbContext db)
    {
        var accounts = new CashAccountService(db);
        var bank = await accounts.CreateAsync(new CreateCashAccountRequest("Bank", "Bank", null, 1000m, new DateOnly(2026, 1, 1)));
        var (tools, _) = await new TransactionCategoryService(db).CreateAsync(
            new CreateTransactionCategoryRequest("Tools & Equipment", "Out", "Operating", null, false));

        return (new PlannedTransactionService(db), bank, tools!);
    }

    private static CreatePlannedTransactionRequest NewPlanned(Guid accountId, Guid categoryId, string? scenarioTag = null) =>
        new("New lift", "Out", 8000m, new DateOnly(2026, 9, 1), categoryId, accountId, scenarioTag);

    [Fact]
    public async Task CreateAsync_ValidatesInputs()
    {
        await using var db = CreateContext();
        var (service, bank, tools) = await SeedAsync(db);

        var (_, badDirection) = await service.CreateAsync(NewPlanned(bank.Id, tools.Id) with { Direction = "Sideways" });
        var (_, badAmount) = await service.CreateAsync(NewPlanned(bank.Id, tools.Id) with { Amount = 0m });
        var (_, badScenario) = await service.CreateAsync(NewPlanned(bank.Id, tools.Id, scenarioTag: "Maybe"));
        var (_, badCategory) = await service.CreateAsync(NewPlanned(bank.Id, Guid.NewGuid()));
        var (_, mismatch) = await service.CreateAsync(NewPlanned(bank.Id, tools.Id) with { Direction = "In" });

        Assert.Equal(PlannedTransactionWriteError.InvalidDirection, badDirection);
        Assert.Equal(PlannedTransactionWriteError.NonPositiveAmount, badAmount);
        Assert.Equal(PlannedTransactionWriteError.InvalidScenarioTag, badScenario);
        Assert.Equal(PlannedTransactionWriteError.CategoryNotFound, badCategory);
        Assert.Equal(PlannedTransactionWriteError.DirectionMismatchesCategory, mismatch);
    }

    [Fact]
    public async Task CreateAsync_AllowsNullAccount()
    {
        await using var db = CreateContext();
        var (service, _, tools) = await SeedAsync(db);

        var (created, error) = await service.CreateAsync(NewPlanned(Guid.Empty, tools.Id) with { AccountId = null });

        Assert.Equal(PlannedTransactionWriteError.None, error);
        Assert.Null(created!.AccountId);
        Assert.Equal("Planned", created.Status);
    }

    [Fact]
    public async Task UpdateAsync_RefusesOnceTerminal()
    {
        await using var db = CreateContext();
        var (service, bank, tools) = await SeedAsync(db);
        var (created, _) = await service.CreateAsync(NewPlanned(bank.Id, tools.Id));
        await service.UpdateAsync(created!.Id,
            new UpdatePlannedTransactionRequest("New lift", "Out", 8000m, created.ExpectedDate, tools.Id, bank.Id, null, "Cancelled"));

        var (_, error) = await service.UpdateAsync(created.Id,
            new UpdatePlannedTransactionRequest("New lift", "Out", 9000m, created.ExpectedDate, tools.Id, bank.Id, null, "Planned"));

        Assert.Equal(PlannedTransactionWriteError.NotEditableOnceTerminal, error);
    }

    [Fact]
    public async Task DeleteAsync_RefusesOnceTerminal()
    {
        await using var db = CreateContext();
        var (service, bank, tools) = await SeedAsync(db);
        var (created, _) = await service.CreateAsync(NewPlanned(bank.Id, tools.Id));
        await service.UpdateAsync(created!.Id,
            new UpdatePlannedTransactionRequest("New lift", "Out", 8000m, created.ExpectedDate, tools.Id, bank.Id, null, "Posted"));

        var error = await service.DeleteAsync(created.Id);

        Assert.Equal(PlannedTransactionWriteError.NotEditableOnceTerminal, error);
    }

    [Fact]
    public async Task DeleteAsync_RemovesPlannedRow()
    {
        await using var db = CreateContext();
        var (service, bank, tools) = await SeedAsync(db);
        var (created, _) = await service.CreateAsync(NewPlanned(bank.Id, tools.Id));

        var error = await service.DeleteAsync(created!.Id);

        Assert.Equal(PlannedTransactionWriteError.None, error);
        Assert.Empty(await db.PlannedTransactions.ToListAsync());
    }
}
