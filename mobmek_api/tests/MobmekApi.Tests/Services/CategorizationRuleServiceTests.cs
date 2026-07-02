using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class CategorizationRuleServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static CategorizationRuleService CreateService(AppDbContext db) => new(db, new CashFlowAuditService(db));

    private static async Task<(CashAccountDto Bank, TransactionCategoryDto Parts, TransactionCategoryDto Fuel)> SeedAsync(AppDbContext db)
    {
        var bank = await new CashAccountService(db).CreateAsync(
            new CreateCashAccountRequest("Bank", "Bank", null, 1000m, new DateOnly(2026, 1, 1)));
        var categories = new TransactionCategoryService(db);
        var (parts, _) = await categories.CreateAsync(new CreateTransactionCategoryRequest("Parts & Materials", "Out", "Operating", null, false));
        var (fuel, _) = await categories.CreateAsync(new CreateTransactionCategoryRequest("Vehicle & Fuel", "Out", "Operating", null, false));
        return (bank, parts!, fuel!);
    }

    private static CreateCategorizationRuleRequest NewRule(Guid categoryId, string matchValue = "z energy",
        string field = "Either", string type = "Contains", int priority = 10) =>
        new($"Rule {matchValue}", priority, field, type, matchValue, null, null, null, categoryId, null, null);

    [Fact]
    public async Task CreateAsync_Validates()
    {
        await using var db = CreateContext();
        var (_, parts, _) = await SeedAsync(db);
        var service = CreateService(db);

        var (_, badField) = await service.CreateAsync(NewRule(parts.Id) with { MatchField = "Vibes" });
        var (_, badType) = await service.CreateAsync(NewRule(parts.Id) with { MatchType = "Rhymes" });
        var (_, badDirection) = await service.CreateAsync(NewRule(parts.Id) with { Direction = "Sideways" });
        var (_, badBand) = await service.CreateAsync(NewRule(parts.Id) with { AmountMin = 100m, AmountMax = 50m });
        var (_, badCategory) = await service.CreateAsync(NewRule(Guid.NewGuid()));
        var (_, badPayee) = await service.CreateAsync(NewRule(parts.Id) with { SetPayeeId = Guid.NewGuid() });
        var (created, ok) = await service.CreateAsync(NewRule(parts.Id));

        Assert.Equal(CategorizationRuleWriteError.InvalidMatchField, badField);
        Assert.Equal(CategorizationRuleWriteError.InvalidMatchType, badType);
        Assert.Equal(CategorizationRuleWriteError.InvalidDirection, badDirection);
        Assert.Equal(CategorizationRuleWriteError.InvalidAmountBand, badBand);
        Assert.Equal(CategorizationRuleWriteError.CategoryNotFound, badCategory);
        Assert.Equal(CategorizationRuleWriteError.PayeeNotFound, badPayee);
        Assert.Equal(CategorizationRuleWriteError.None, ok);
        Assert.Equal("Parts & Materials", created!.SetCategoryName);
    }

    [Fact]
    public async Task SuggestAsync_LowestPriorityWins_AndRespectsConstraints()
    {
        await using var db = CreateContext();
        var (_, parts, fuel) = await SeedAsync(db);
        var service = CreateService(db);
        await service.CreateAsync(NewRule(parts.Id, "energy", priority: 20));
        await service.CreateAsync(NewRule(fuel.Id, "z energy", priority: 5));
        var (inactive, _) = await service.CreateAsync(NewRule(parts.Id, "z energy", priority: 1) with { IsActive = false });

        var winner = await service.SuggestAsync(new RuleSuggestionRequest("Z Energy Albany", null, "Out", 80m));
        var none = await service.SuggestAsync(new RuleSuggestionRequest("Countdown", null, null, null));

        Assert.NotNull(winner);
        Assert.Equal(fuel.Id, winner!.CategoryId);       // priority 5 beats 20; inactive priority 1 ignored
        Assert.NotEqual(inactive!.Id, winner.RuleId);
        Assert.Null(none);
    }

    [Theory]
    [InlineData("StartsWith", "Z Energy Albany", true)]
    [InlineData("StartsWith", "Shell / Z Energy", false)]
    [InlineData("Equals", "z energy", true)]
    [InlineData("Equals", "z energy albany", false)]
    [InlineData("Contains", "paid at Z ENERGY today", true)]
    public async Task SuggestAsync_MatchTypes(string matchType, string description, bool expectMatch)
    {
        await using var db = CreateContext();
        var (_, parts, _) = await SeedAsync(db);
        var service = CreateService(db);
        await service.CreateAsync(NewRule(parts.Id, "z energy", field: "Description", type: matchType));

        var suggestion = await service.SuggestAsync(new RuleSuggestionRequest(description, null, null, null));

        Assert.Equal(expectMatch, suggestion is not null);
    }

    [Fact]
    public async Task SuggestAsync_AmountBandAndDirection()
    {
        await using var db = CreateContext();
        var (_, parts, _) = await SeedAsync(db);
        var service = CreateService(db);
        await service.CreateAsync(NewRule(parts.Id, "repco") with { Direction = "Out", AmountMin = 10m, AmountMax = 100m });

        Assert.NotNull(await service.SuggestAsync(new RuleSuggestionRequest("Repco", null, "Out", 50m)));
        Assert.Null(await service.SuggestAsync(new RuleSuggestionRequest("Repco", null, "In", 50m)));
        Assert.Null(await service.SuggestAsync(new RuleSuggestionRequest("Repco", null, "Out", 500m)));
        // Unknown amount/direction can't disqualify — the rule still offers itself.
        Assert.NotNull(await service.SuggestAsync(new RuleSuggestionRequest("Repco", null, null, null)));
    }

    [Fact]
    public async Task ApplyToExistingAsync_PreviewCountsWithoutChanging_CommitRewrites()
    {
        await using var db = CreateContext();
        var (bank, parts, fuel) = await SeedAsync(db);
        var ruleService = CreateService(db);
        var transactions = new CashTransactionService(db, new LocalFileStorage(Path.GetTempPath()), new CashFlowAuditService(db));

        var (match1, _) = await transactions.CreateAsync(new CreateCashTransactionRequest(
            bank.Id, "Out", 80m, new DateOnly(2026, 6, 1), "Z Energy Albany", parts.Id, null, null, null, null, null));
        var (alreadyRight, _) = await transactions.CreateAsync(new CreateCashTransactionRequest(
            bank.Id, "Out", 60m, new DateOnly(2026, 6, 2), "Z Energy Henderson", fuel.Id, null, null, null, null, null));
        await transactions.CreateAsync(new CreateCashTransactionRequest(
            bank.Id, "Out", 30m, new DateOnly(2026, 6, 3), "Countdown", parts.Id, null, null, null, null, null));

        var (rule, _) = await ruleService.CreateAsync(NewRule(fuel.Id, "z energy"));

        var (preview, _) = await ruleService.ApplyToExistingAsync(rule!.Id, commit: false);
        Assert.Equal(2, preview!.MatchCount);
        Assert.Equal(1, preview.UpdatedCount);           // one already has the right category
        Assert.Equal(parts.Id, (await transactions.GetByIdAsync(match1!.Id))!.CategoryId);   // preview changed nothing

        var (committed, _) = await ruleService.ApplyToExistingAsync(rule.Id, commit: true);
        Assert.Equal(1, committed!.UpdatedCount);
        Assert.Equal(fuel.Id, (await transactions.GetByIdAsync(match1.Id))!.CategoryId);
        Assert.Equal(fuel.Id, (await transactions.GetByIdAsync(alreadyRight!.Id))!.CategoryId);

        var (_, notFound) = await ruleService.ApplyToExistingAsync(Guid.NewGuid(), commit: true);
        Assert.Equal(CategorizationRuleWriteError.NotFound, notFound);
    }

    [Fact]
    public async Task ApplyToExistingAsync_SkipsManagedReconciledAndLockedRows()
    {
        await using var db = CreateContext();
        var (bank, parts, fuel) = await SeedAsync(db);
        var ruleService = CreateService(db);
        var transactions = new CashTransactionService(db, new LocalFileStorage(Path.GetTempPath()), new CashFlowAuditService(db));

        var invoicePosted = new CashTransaction
        {
            AccountId = bank.Id, Direction = "In", Amount = 100m, Date = new DateOnly(2026, 6, 6),
            Description = "Z Energy refund", CategoryId = parts.Id, InvoiceId = Guid.NewGuid(),
        };
        db.CashTransactions.Add(invoicePosted);
        await db.SaveChangesAsync();

        var (reconciled, _) = await transactions.CreateAsync(new CreateCashTransactionRequest(
            bank.Id, "Out", 80m, new DateOnly(2026, 6, 1), "Z Energy Albany", parts.Id, null, null, null, null, null));
        var entity = await db.CashTransactions.SingleAsync(t => t.Id == reconciled!.Id);
        entity.Status = "Reconciled";
        await db.SaveChangesAsync();

        var (locked, _) = await transactions.CreateAsync(new CreateCashTransactionRequest(
            bank.Id, "Out", 70m, new DateOnly(2026, 3, 1), "Z Energy old", parts.Id, null, null, null, null, null));
        await new CashFlowSettingsService(db, new CashFlowAuditService(db)).UpdateAsync(
            new UpdateCashFlowSettingsRequest(null, null, null, null, 0m, new DateOnly(2026, 3, 31)));

        var (rule, _) = await ruleService.CreateAsync(NewRule(fuel.Id, "z energy"));
        var (result, _) = await ruleService.ApplyToExistingAsync(rule!.Id, commit: true);

        Assert.Equal(0, result!.MatchCount);   // all three candidates were protected
        Assert.Equal(parts.Id, (await transactions.GetByIdAsync(locked!.Id))!.CategoryId);
    }

    [Fact]
    public async Task UpdateAndDelete_Work()
    {
        await using var db = CreateContext();
        var (_, parts, fuel) = await SeedAsync(db);
        var service = CreateService(db);
        var (rule, _) = await service.CreateAsync(NewRule(parts.Id));

        var (updated, error) = await service.UpdateAsync(rule!.Id, new UpdateCategorizationRuleRequest(
            "Renamed", 3, "Description", "StartsWith", "bp connect", "Out", null, null, fuel.Id, "Taxable", null, false));

        Assert.Equal(CategorizationRuleWriteError.None, error);
        Assert.Equal("Renamed", updated!.Name);
        Assert.Equal(fuel.Id, updated.SetCategoryId);
        Assert.False(updated.IsActive);

        Assert.True(await service.DeleteAsync(rule.Id));
        Assert.False(await service.DeleteAsync(rule.Id));
    }
}
