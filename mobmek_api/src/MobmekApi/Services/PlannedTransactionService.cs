using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class PlannedTransactionService(AppDbContext db) : IPlannedTransactionService
{
    private static readonly string[] ValidDirections = ["In", "Out"];
    private static readonly string[] ValidScenarioTags = ["BestCase", "WorstCase"];
    private static readonly string[] ValidStatuses = ["Planned", "Posted", "Cancelled"];

    public async Task<IReadOnlyList<PlannedTransactionDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var planned = await db.PlannedTransactions.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Account)
            .OrderBy(p => p.ExpectedDate)
            .ToListAsync(cancellationToken);

        return planned.Select(ToDto).ToList();
    }

    public async Task<PlannedTransactionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var planned = await db.PlannedTransactions.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return planned is null ? null : ToDto(planned);
    }

    public async Task<(PlannedTransactionDto? Planned, PlannedTransactionWriteError Error)> CreateAsync(
        CreatePlannedTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var error = await ValidateAsync(
            request.AccountId, request.CategoryId, request.Direction, request.Amount, request.ScenarioTag, cancellationToken);
        if (error != PlannedTransactionWriteError.None)
        {
            return (null, error);
        }

        var planned = new PlannedTransaction
        {
            Description = request.Description,
            Direction = request.Direction,
            Amount = request.Amount,
            ExpectedDate = request.ExpectedDate,
            CategoryId = request.CategoryId,
            AccountId = request.AccountId,
            ScenarioTag = request.ScenarioTag,
        };

        db.PlannedTransactions.Add(planned);
        await db.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(planned.Id, cancellationToken), PlannedTransactionWriteError.None);
    }

    public async Task<(PlannedTransactionDto? Planned, PlannedTransactionWriteError Error)> UpdateAsync(
        Guid id, UpdatePlannedTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var planned = await db.PlannedTransactions.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (planned is null)
        {
            return (null, PlannedTransactionWriteError.NotFound);
        }

        if (planned.Status != "Planned")
        {
            return (null, PlannedTransactionWriteError.NotEditableOnceTerminal);
        }

        if (!ValidStatuses.Contains(request.Status))
        {
            return (null, PlannedTransactionWriteError.InvalidStatus);
        }

        var error = await ValidateAsync(
            request.AccountId, request.CategoryId, request.Direction, request.Amount, request.ScenarioTag, cancellationToken);
        if (error != PlannedTransactionWriteError.None)
        {
            return (null, error);
        }

        planned.Description = request.Description;
        planned.Direction = request.Direction;
        planned.Amount = request.Amount;
        planned.ExpectedDate = request.ExpectedDate;
        planned.CategoryId = request.CategoryId;
        planned.AccountId = request.AccountId;
        planned.ScenarioTag = request.ScenarioTag;
        planned.Status = request.Status;
        await db.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(id, cancellationToken), PlannedTransactionWriteError.None);
    }

    public async Task<PlannedTransactionWriteError> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var planned = await db.PlannedTransactions.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (planned is null)
        {
            return PlannedTransactionWriteError.NotFound;
        }

        if (planned.Status != "Planned")
        {
            return PlannedTransactionWriteError.NotEditableOnceTerminal;
        }

        db.PlannedTransactions.Remove(planned);
        await db.SaveChangesAsync(cancellationToken);

        return PlannedTransactionWriteError.None;
    }

    private async Task<PlannedTransactionWriteError> ValidateAsync(
        Guid? accountId, Guid categoryId, string direction, decimal amount, string? scenarioTag, CancellationToken cancellationToken)
    {
        if (!ValidDirections.Contains(direction))
        {
            return PlannedTransactionWriteError.InvalidDirection;
        }

        if (amount <= 0)
        {
            return PlannedTransactionWriteError.NonPositiveAmount;
        }

        if (scenarioTag is not null && !ValidScenarioTags.Contains(scenarioTag))
        {
            return PlannedTransactionWriteError.InvalidScenarioTag;
        }

        if (accountId is not null)
        {
            var account = await db.CashAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
            if (account is null)
            {
                return PlannedTransactionWriteError.AccountNotFound;
            }

            if (account.IsArchived)
            {
                return PlannedTransactionWriteError.AccountArchived;
            }
        }

        var category = await db.TransactionCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken);
        if (category is null)
        {
            return PlannedTransactionWriteError.CategoryNotFound;
        }

        if (category.Direction != "Either" && category.Direction != direction)
        {
            return PlannedTransactionWriteError.DirectionMismatchesCategory;
        }

        return PlannedTransactionWriteError.None;
    }

    private static PlannedTransactionDto ToDto(PlannedTransaction p) =>
        new(p.Id, p.Description, p.Direction, p.Amount, p.ExpectedDate, p.CategoryId, p.Category?.Name ?? string.Empty,
            p.AccountId, p.Account?.Name, p.Status, p.ScenarioTag, p.CreatedAtUtc, p.UpdatedAtUtc);
}
