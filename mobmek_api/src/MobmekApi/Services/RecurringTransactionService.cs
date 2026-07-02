using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class RecurringTransactionService(AppDbContext db) : IRecurringTransactionService
{
    private static readonly string[] ValidDirections = ["In", "Out"];
    private static readonly string[] ValidGstTreatments = ["Taxable", "Exempt", "ZeroRated"];

    // How far ahead of "today" we search for a schedule's next occurrence in list views.
    private const int NextOccurrenceSearchDays = 366;

    public async Task<IReadOnlyList<RecurringTransactionDto>> GetAllAsync(
        bool includePaused = true, CancellationToken cancellationToken = default)
    {
        var query = db.RecurringTransactions.AsNoTracking()
            .Include(r => r.Account)
            .Include(r => r.Category)
            .AsQueryable();

        if (!includePaused)
        {
            query = query.Where(r => !r.IsPaused);
        }

        var recurring = await query.OrderBy(r => r.Description).ToListAsync(cancellationToken);
        var postedDates = await PostedDatesByScheduleAsync(recurring.Select(r => r.Id), cancellationToken);
        var today = Today();

        return recurring.Select(r => ToDto(r, postedDates.GetValueOrDefault(r.Id, []), today)).ToList();
    }

    public async Task<RecurringTransactionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var recurring = await db.RecurringTransactions.AsNoTracking()
            .Include(r => r.Account)
            .Include(r => r.Category)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (recurring is null)
        {
            return null;
        }

        var posted = await PostedDatesAsync(id, cancellationToken);
        return ToDto(recurring, posted, Today());
    }

    public async Task<(RecurringTransactionDto? Recurring, RecurringTransactionWriteError Error)> CreateAsync(
        CreateRecurringTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var (category, error) = await ValidateAsync(
            request.AccountId, request.CategoryId, request.Direction, request.Amount,
            request.GstTreatment, request.Frequency, request.Interval, cancellationToken);
        if (error != RecurringTransactionWriteError.None)
        {
            return (null, error);
        }

        var recurring = new RecurringTransaction
        {
            Description = request.Description,
            Direction = request.Direction,
            Amount = request.Amount,
            CategoryId = request.CategoryId,
            AccountId = request.AccountId,
            Counterparty = request.Counterparty,
            GstTreatment = request.GstTreatment ?? category!.DefaultGstTreatment,
            Frequency = request.Frequency,
            Interval = request.Interval,
            AnchorDate = request.AnchorDate,
            EndDate = request.EndDate,
            AutoPost = request.AutoPost,
        };

        db.RecurringTransactions.Add(recurring);
        await db.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(recurring.Id, cancellationToken), RecurringTransactionWriteError.None);
    }

    public async Task<(RecurringTransactionDto? Recurring, RecurringTransactionWriteError Error)> UpdateAsync(
        Guid id, UpdateRecurringTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var recurring = await db.RecurringTransactions.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (recurring is null)
        {
            return (null, RecurringTransactionWriteError.NotFound);
        }

        var (category, error) = await ValidateAsync(
            request.AccountId, request.CategoryId, request.Direction, request.Amount,
            request.GstTreatment, request.Frequency, request.Interval, cancellationToken);
        if (error != RecurringTransactionWriteError.None)
        {
            return (null, error);
        }

        recurring.Description = request.Description;
        recurring.Direction = request.Direction;
        recurring.Amount = request.Amount;
        recurring.CategoryId = request.CategoryId;
        recurring.AccountId = request.AccountId;
        recurring.Counterparty = request.Counterparty;
        recurring.GstTreatment = request.GstTreatment ?? category!.DefaultGstTreatment;
        recurring.Frequency = request.Frequency;
        recurring.Interval = request.Interval;
        recurring.AnchorDate = request.AnchorDate;
        recurring.EndDate = request.EndDate;
        recurring.AutoPost = request.AutoPost;
        recurring.IsPaused = request.IsPaused;
        await db.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(id, cancellationToken), RecurringTransactionWriteError.None);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var recurring = await db.RecurringTransactions.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (recurring is null)
        {
            return false;
        }

        db.RecurringTransactions.Remove(recurring);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<RecurringTransactionDto?> SetPausedAsync(Guid id, bool paused, CancellationToken cancellationToken = default)
    {
        var recurring = await db.RecurringTransactions.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (recurring is null)
        {
            return null;
        }

        recurring.IsPaused = paused;
        await db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<DueOccurrenceDto>> GetDueOccurrencesAsync(
        DateOnly asOfDate, bool autoPostOnly = false, CancellationToken cancellationToken = default)
    {
        var query = db.RecurringTransactions.AsNoTracking().Include(r => r.Account).Where(r => !r.IsPaused);
        if (autoPostOnly)
        {
            query = query.Where(r => r.AutoPost);
        }

        var recurring = await query.ToListAsync(cancellationToken);
        var postedDates = await PostedDatesByScheduleAsync(recurring.Select(r => r.Id), cancellationToken);

        var due = new List<DueOccurrenceDto>();
        foreach (var r in recurring)
        {
            var posted = postedDates.GetValueOrDefault(r.Id, []);
            foreach (var date in RecurringOccurrences.Expand(r.Frequency, r.Interval, r.AnchorDate, r.EndDate, r.AnchorDate, asOfDate))
            {
                if (!posted.Contains(date))
                {
                    due.Add(new DueOccurrenceDto(r.Id, r.Description, r.Direction, r.Amount, r.AccountId, r.Account?.Name ?? string.Empty, date));
                }
            }
        }

        return due.OrderBy(d => d.Date).ToList();
    }

    public async Task<(CashTransactionDto? Transaction, RecurringTransactionWriteError Error)> PostOccurrenceAsync(
        Guid id, DateOnly date, CancellationToken cancellationToken = default)
    {
        var recurring = await db.RecurringTransactions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (recurring is null)
        {
            return (null, RecurringTransactionWriteError.NotFound);
        }

        var isRealOccurrence = RecurringOccurrences
            .Expand(recurring.Frequency, recurring.Interval, recurring.AnchorDate, recurring.EndDate, date, date)
            .Any();
        if (!isRealOccurrence)
        {
            return (null, RecurringTransactionWriteError.OccurrenceNotDue);
        }

        var alreadyPosted = await db.CashTransactions
            .AnyAsync(t => t.RecurringTransactionId == id && t.Date == date, cancellationToken);
        if (alreadyPosted)
        {
            return (null, RecurringTransactionWriteError.OccurrenceAlreadyPosted);
        }

        var transaction = new CashTransaction
        {
            AccountId = recurring.AccountId,
            Direction = recurring.Direction,
            Amount = recurring.Amount,
            Date = date,
            Description = recurring.Description,
            CategoryId = recurring.CategoryId,
            Counterparty = recurring.Counterparty,
            GstTreatment = recurring.GstTreatment,
            RecurringTransactionId = recurring.Id,
        };

        db.CashTransactions.Add(transaction);
        await db.SaveChangesAsync(cancellationToken);

        var dto = await db.CashTransactions.AsNoTracking()
            .Include(t => t.Account).Include(t => t.Category).Include(t => t.Attachments)
            .Where(t => t.Id == transaction.Id)
            .Select(t => new CashTransactionDto(
                t.Id, t.AccountId, t.Account!.Name, t.Direction, t.Amount, t.Date, t.Description,
                t.CategoryId, t.Category!.Name, t.PayeeId, t.Counterparty, t.Status,
                t.InvoiceId, null, t.TransferGroupId, t.SplitGroupId,
                t.GstTreatment, t.Notes, new List<TransactionAttachmentDto>(), t.CreatedAtUtc, t.UpdatedAtUtc, null))
            .FirstAsync(cancellationToken);

        return (dto, RecurringTransactionWriteError.None);
    }

    private async Task<(TransactionCategory? Category, RecurringTransactionWriteError Error)> ValidateAsync(
        Guid accountId, Guid categoryId, string direction, decimal amount, string? gstTreatment,
        string frequency, int interval, CancellationToken cancellationToken)
    {
        if (!ValidDirections.Contains(direction))
        {
            return (null, RecurringTransactionWriteError.InvalidDirection);
        }

        if (amount <= 0)
        {
            return (null, RecurringTransactionWriteError.NonPositiveAmount);
        }

        if (gstTreatment is not null && !ValidGstTreatments.Contains(gstTreatment))
        {
            return (null, RecurringTransactionWriteError.InvalidGstTreatment);
        }

        if (!RecurringOccurrences.ValidFrequencies.Contains(frequency))
        {
            return (null, RecurringTransactionWriteError.InvalidFrequency);
        }

        if (interval < 1)
        {
            return (null, RecurringTransactionWriteError.InvalidInterval);
        }

        var account = await db.CashAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        if (account is null)
        {
            return (null, RecurringTransactionWriteError.AccountNotFound);
        }

        if (account.IsArchived)
        {
            return (null, RecurringTransactionWriteError.AccountArchived);
        }

        var category = await db.TransactionCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken);
        if (category is null)
        {
            return (null, RecurringTransactionWriteError.CategoryNotFound);
        }

        if (category.Direction != "Either" && category.Direction != direction)
        {
            return (null, RecurringTransactionWriteError.DirectionMismatchesCategory);
        }

        return (category, RecurringTransactionWriteError.None);
    }

    private async Task<Dictionary<Guid, HashSet<DateOnly>>> PostedDatesByScheduleAsync(
        IEnumerable<Guid> recurringIds, CancellationToken cancellationToken)
    {
        var ids = recurringIds.ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = await db.CashTransactions.AsNoTracking()
            .Where(t => t.RecurringTransactionId != null && ids.Contains(t.RecurringTransactionId!.Value))
            .Select(t => new { t.RecurringTransactionId, t.Date })
            .ToListAsync(cancellationToken);

        return rows.GroupBy(r => r.RecurringTransactionId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Date).ToHashSet());
    }

    private async Task<HashSet<DateOnly>> PostedDatesAsync(Guid recurringId, CancellationToken cancellationToken) =>
        (await db.CashTransactions.AsNoTracking()
            .Where(t => t.RecurringTransactionId == recurringId)
            .Select(t => t.Date)
            .ToListAsync(cancellationToken))
        .ToHashSet();

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    private RecurringTransactionDto ToDto(RecurringTransaction r, HashSet<DateOnly> posted, DateOnly today)
    {
        DateOnly? next = null;
        if (!r.IsPaused)
        {
            var searchFrom = r.AnchorDate > today ? r.AnchorDate : today;
            next = RecurringOccurrences
                .Expand(r.Frequency, r.Interval, r.AnchorDate, r.EndDate, searchFrom, today.AddDays(NextOccurrenceSearchDays))
                .FirstOrDefault(d => !posted.Contains(d));
            if (next == default)
            {
                next = null;
            }
        }

        return new RecurringTransactionDto(
            r.Id, r.Description, r.Direction, r.Amount, r.CategoryId, r.Category?.Name ?? string.Empty,
            r.AccountId, r.Account?.Name ?? string.Empty, r.Counterparty, r.GstTreatment,
            r.Frequency, r.Interval, r.AnchorDate, r.EndDate, r.AutoPost, r.IsPaused,
            next, RecurringOccurrences.MonthlyEquivalent(r.Amount, r.Frequency, r.Interval),
            r.CreatedAtUtc, r.UpdatedAtUtc);
    }
}
