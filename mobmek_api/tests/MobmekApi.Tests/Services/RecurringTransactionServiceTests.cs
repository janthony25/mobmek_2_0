using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class RecurringTransactionServiceTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static async Task<(RecurringTransactionService Service, CashAccountDto Bank, TransactionCategoryDto Rent)> SeedAsync(AppDbContext db)
    {
        var accounts = new CashAccountService(db);
        var bank = await accounts.CreateAsync(new CreateCashAccountRequest("Bank", "Bank", null, 1000m, new DateOnly(2026, 1, 1)));
        var (rent, _) = await new TransactionCategoryService(db).CreateAsync(
            new CreateTransactionCategoryRequest("Rent", "Out", "Operating", null, false));

        return (new RecurringTransactionService(db), bank, rent!);
    }

    private static CreateRecurringTransactionRequest NewSchedule(
        Guid accountId, Guid categoryId, DateOnly anchorDate, string frequency = "Monthly", int interval = 1, DateOnly? endDate = null, bool autoPost = false) =>
        new("Rent", "Out", 500m, categoryId, accountId, "Landlord", null, frequency, interval, anchorDate, endDate, autoPost);

    [Fact]
    public async Task CreateAsync_ValidatesInputs()
    {
        await using var db = CreateContext();
        var (service, bank, rent) = await SeedAsync(db);

        var (_, badDirection) = await service.CreateAsync(NewSchedule(bank.Id, rent.Id, Today) with { Direction = "Sideways" });
        var (_, badAmount) = await service.CreateAsync(NewSchedule(bank.Id, rent.Id, Today) with { Amount = 0m });
        var (_, badFrequency) = await service.CreateAsync(NewSchedule(bank.Id, rent.Id, Today) with { Frequency = "Daily" });
        var (_, badInterval) = await service.CreateAsync(NewSchedule(bank.Id, rent.Id, Today) with { Interval = 0 });
        var (_, badAccount) = await service.CreateAsync(NewSchedule(Guid.NewGuid(), rent.Id, Today));
        var (_, badCategory) = await service.CreateAsync(NewSchedule(bank.Id, Guid.NewGuid(), Today));
        var (_, mismatch) = await service.CreateAsync(NewSchedule(bank.Id, rent.Id, Today) with { Direction = "In" });

        Assert.Equal(RecurringTransactionWriteError.InvalidDirection, badDirection);
        Assert.Equal(RecurringTransactionWriteError.NonPositiveAmount, badAmount);
        Assert.Equal(RecurringTransactionWriteError.InvalidFrequency, badFrequency);
        Assert.Equal(RecurringTransactionWriteError.InvalidInterval, badInterval);
        Assert.Equal(RecurringTransactionWriteError.AccountNotFound, badAccount);
        Assert.Equal(RecurringTransactionWriteError.CategoryNotFound, badCategory);
        Assert.Equal(RecurringTransactionWriteError.DirectionMismatchesCategory, mismatch);
    }

    [Fact]
    public async Task CreateAsync_DefaultsGstTreatmentFromCategory()
    {
        await using var db = CreateContext();
        var (service, bank, rent) = await SeedAsync(db);

        var (created, error) = await service.CreateAsync(NewSchedule(bank.Id, rent.Id, Today));

        Assert.Equal(RecurringTransactionWriteError.None, error);
        Assert.Equal("Taxable", created!.GstTreatment);
        Assert.Equal("Bank", created.AccountName);
        Assert.Equal("Rent", created.CategoryName);
    }

    [Fact]
    public async Task GetAllAsync_ComputesMonthlyEquivalentAndNextOccurrence()
    {
        await using var db = CreateContext();
        var (service, bank, rent) = await SeedAsync(db);
        var anchor = Today.AddMonths(-2); // two occurrences already in the past, none posted
        await service.CreateAsync(NewSchedule(bank.Id, rent.Id, anchor));

        var all = await service.GetAllAsync();

        var dto = Assert.Single(all);
        Assert.Equal(500m, dto.MonthlyEquivalentAmount);
        Assert.NotNull(dto.NextOccurrenceDate);
        Assert.True(dto.NextOccurrenceDate <= Today); // the earliest un-posted occurrence, which is overdue
    }

    [Fact]
    public async Task SetPausedAsync_ClearsNextOccurrenceDate()
    {
        await using var db = CreateContext();
        var (service, bank, rent) = await SeedAsync(db);
        var (created, _) = await service.CreateAsync(NewSchedule(bank.Id, rent.Id, Today));

        var paused = await service.SetPausedAsync(created!.Id, true);

        Assert.True(paused!.IsPaused);
        Assert.Null(paused.NextOccurrenceDate);
    }

    [Fact]
    public async Task GetDueOccurrencesAsync_ExcludesPausedAndAlreadyPosted()
    {
        await using var db = CreateContext();
        var (service, bank, rent) = await SeedAsync(db);
        var anchor = Today.AddMonths(-1);
        var (due, _) = await service.CreateAsync(NewSchedule(bank.Id, rent.Id, anchor));
        var (paused, _) = await service.CreateAsync(NewSchedule(bank.Id, rent.Id, anchor) with { Description = "Paused one" });
        await service.SetPausedAsync(paused!.Id, true);

        await service.PostOccurrenceAsync(due!.Id, anchor); // post the first occurrence, leaving the current-month one due

        var occurrences = await service.GetDueOccurrencesAsync(Today);

        var occurrence = Assert.Single(occurrences);
        Assert.Equal(due.Id, occurrence.RecurringTransactionId);
        Assert.Equal(anchor.AddMonths(1), occurrence.Date);
    }

    [Fact]
    public async Task GetDueOccurrencesAsync_AutoPostOnlyFiltersToAutoPostSchedules()
    {
        await using var db = CreateContext();
        var (service, bank, rent) = await SeedAsync(db);
        var anchor = Today.AddMonths(-1);
        await service.CreateAsync(NewSchedule(bank.Id, rent.Id, anchor, autoPost: false));
        await service.CreateAsync(NewSchedule(bank.Id, rent.Id, anchor, autoPost: true) with { Description = "Auto" });

        var occurrences = await service.GetDueOccurrencesAsync(Today, autoPostOnly: true);

        Assert.NotEmpty(occurrences);
        Assert.All(occurrences, o => Assert.Equal("Auto", o.Description));
    }

    [Fact]
    public async Task PostOccurrenceAsync_CreatesLinkedCashTransaction()
    {
        await using var db = CreateContext();
        var (service, bank, rent) = await SeedAsync(db);
        var (created, _) = await service.CreateAsync(NewSchedule(bank.Id, rent.Id, Today));

        var (transaction, error) = await service.PostOccurrenceAsync(created!.Id, Today);

        Assert.Equal(RecurringTransactionWriteError.None, error);
        Assert.Equal(created.Id, (await db.CashTransactions.FirstAsync()).RecurringTransactionId);
        Assert.Equal(500m, transaction!.Amount);
        Assert.Equal("Out", transaction.Direction);
    }

    [Fact]
    public async Task PostOccurrenceAsync_RefusesADateThatIsNotAScheduledOccurrence()
    {
        await using var db = CreateContext();
        var (service, bank, rent) = await SeedAsync(db);
        var (created, _) = await service.CreateAsync(NewSchedule(bank.Id, rent.Id, Today));

        var (_, error) = await service.PostOccurrenceAsync(created!.Id, Today.AddDays(3));

        Assert.Equal(RecurringTransactionWriteError.OccurrenceNotDue, error);
    }

    [Fact]
    public async Task PostOccurrenceAsync_RefusesAlreadyPostedOccurrence()
    {
        await using var db = CreateContext();
        var (service, bank, rent) = await SeedAsync(db);
        var (created, _) = await service.CreateAsync(NewSchedule(bank.Id, rent.Id, Today));
        await service.PostOccurrenceAsync(created!.Id, Today);

        var (_, error) = await service.PostOccurrenceAsync(created.Id, Today);

        Assert.Equal(RecurringTransactionWriteError.OccurrenceAlreadyPosted, error);
    }

    [Fact]
    public async Task DeleteAsync_RemovesScheduleButLeavesPostedHistory()
    {
        await using var db = CreateContext();
        var (service, bank, rent) = await SeedAsync(db);
        var (created, _) = await service.CreateAsync(NewSchedule(bank.Id, rent.Id, Today));
        await service.PostOccurrenceAsync(created!.Id, Today);

        var deleted = await service.DeleteAsync(created.Id);

        Assert.True(deleted);
        var transaction = await db.CashTransactions.FirstAsync();
        Assert.Null(transaction.RecurringTransactionId); // history survives, unlinked (FK is SetNull)
    }
}
