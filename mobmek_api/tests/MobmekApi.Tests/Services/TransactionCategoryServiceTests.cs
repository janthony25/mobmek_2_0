using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class TransactionCategoryServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Seeder_CreatesSystemCategories_AndIsIdempotent()
    {
        await using var db = CreateContext();

        await CashFlowSeeder.SeedAsync(db);
        var firstCount = await db.TransactionCategories.CountAsync();
        await CashFlowSeeder.SeedAsync(db);

        Assert.True(firstCount > 20);
        Assert.Equal(firstCount, await db.TransactionCategories.CountAsync());
        var sales = await db.TransactionCategories.SingleAsync(c => c.Name == CashFlowSeeder.WorkshopSalesCategory);
        Assert.True(sales.IsSystem);
        Assert.Equal("In", sales.Direction);
        var transfer = await db.TransactionCategories.SingleAsync(c => c.Name == CashFlowSeeder.TransferCategory);
        Assert.True(transfer.ExcludeFromOperatingExpense);
    }

    [Fact]
    public async Task EnsureSystemCategoryAsync_CreatesWhenUnseeded_ThenReuses()
    {
        await using var db = CreateContext();

        var first = await CashFlowSeeder.EnsureSystemCategoryAsync(db, CashFlowSeeder.WorkshopSalesCategory);
        var second = await CashFlowSeeder.EnsureSystemCategoryAsync(db, CashFlowSeeder.WorkshopSalesCategory);

        Assert.Equal(first.Id, second.Id);
        Assert.True(first.IsSystem);
    }

    [Fact]
    public async Task CreateAsync_ValidatesDirectionGstAndDuplicateName()
    {
        await using var db = CreateContext();
        var service = new TransactionCategoryService(db);

        var (_, badDirection) = await service.CreateAsync(new CreateTransactionCategoryRequest("A", "Sideways", "Operating", null, false));
        var (_, badGst) = await service.CreateAsync(new CreateTransactionCategoryRequest("A", "Out", "Operating", "Full", false));
        var (created, ok) = await service.CreateAsync(new CreateTransactionCategoryRequest("Consumables", "Out", "Operating", null, false));
        var (_, duplicate) = await service.CreateAsync(new CreateTransactionCategoryRequest("Consumables", "Out", "Operating", null, false));

        Assert.Equal(TransactionCategoryWriteError.InvalidDirection, badDirection);
        Assert.Equal(TransactionCategoryWriteError.InvalidGstTreatment, badGst);
        Assert.Equal(TransactionCategoryWriteError.None, ok);
        Assert.Equal("Taxable", created!.DefaultGstTreatment);   // default when omitted
        Assert.False(created.IsSystem);                          // user categories are never system
        Assert.Equal(TransactionCategoryWriteError.DuplicateName, duplicate);
    }

    [Fact]
    public async Task UpdateAsync_OnSystemCategory_AppliesOnlyNameAndArchived()
    {
        await using var db = CreateContext();
        await CashFlowSeeder.SeedAsync(db);
        var service = new TransactionCategoryService(db);
        var rent = (await service.GetAllAsync()).Single(c => c.Name == "Rent");

        var (updated, error) = await service.UpdateAsync(rent.Id,
            new UpdateTransactionCategoryRequest("Workshop Rent", "In", "Sales", "Exempt", true, IsArchived: false));

        Assert.Equal(TransactionCategoryWriteError.None, error);
        Assert.Equal("Workshop Rent", updated!.Name);
        Assert.Equal("Out", updated.Direction);                  // fixed on system rows
        Assert.Equal("Operating", updated.Group);
        Assert.Equal("Taxable", updated.DefaultGstTreatment);
        Assert.False(updated.ExcludeFromOperatingExpense);
    }

    [Fact]
    public async Task UpdateAsync_OnUserCategory_AppliesEverything()
    {
        await using var db = CreateContext();
        var service = new TransactionCategoryService(db);
        var (created, _) = await service.CreateAsync(new CreateTransactionCategoryRequest("Consumables", "Out", "Operating", null, false));

        var (updated, error) = await service.UpdateAsync(created!.Id,
            new UpdateTransactionCategoryRequest("Shop Supplies", "Either", "Other", "Exempt", true, IsArchived: true));

        Assert.Equal(TransactionCategoryWriteError.None, error);
        Assert.Equal("Shop Supplies", updated!.Name);
        Assert.Equal("Either", updated.Direction);
        Assert.Equal("Exempt", updated.DefaultGstTreatment);
        Assert.True(updated.ExcludeFromOperatingExpense);
        Assert.True(updated.IsArchived);
        Assert.NotNull(updated.UpdatedAtUtc);
    }

    [Fact]
    public async Task DeleteAsync_RefusesSystemAndInUseCategories()
    {
        await using var db = CreateContext();
        await CashFlowSeeder.SeedAsync(db);
        var service = new TransactionCategoryService(db);
        var system = (await service.GetAllAsync()).First(c => c.IsSystem);

        var (used, _) = await service.CreateAsync(new CreateTransactionCategoryRequest("Consumables", "Out", "Operating", null, false));
        var account = new CashAccount { Name = "Till", Type = "Cash" };
        db.CashAccounts.Add(account);
        db.CashTransactions.Add(new CashTransaction
        {
            AccountId = account.Id, Direction = "Out", Amount = 5m,
            Date = new DateOnly(2026, 3, 1), Description = "Rags", CategoryId = used!.Id,
        });
        await db.SaveChangesAsync();

        Assert.Equal(TransactionCategoryWriteError.SystemCategory, await service.DeleteAsync(system.Id));
        Assert.Equal(TransactionCategoryWriteError.InUse, await service.DeleteAsync(used.Id));
        Assert.Equal(TransactionCategoryWriteError.NotFound, await service.DeleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAsync_RemovesUnusedUserCategory()
    {
        await using var db = CreateContext();
        var service = new TransactionCategoryService(db);
        var (created, _) = await service.CreateAsync(new CreateTransactionCategoryRequest("Consumables", "Out", "Operating", null, false));

        Assert.Equal(TransactionCategoryWriteError.None, await service.DeleteAsync(created!.Id));
        Assert.Null(await service.GetByIdAsync(created.Id));
    }
}
