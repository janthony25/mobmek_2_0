using System.Globalization;
using System.Text;
using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class CashTransactionService(AppDbContext db, IFileStorage fileStorage, ICashFlowAuditService audit) : ICashTransactionService
{
    private static readonly string[] ValidDirections = ["In", "Out"];
    private static readonly string[] ValidGstTreatments = ["Taxable", "Exempt", "ZeroRated"];

    // "Reconciled" is set exclusively by completing a reconciliation, never by hand.
    private static readonly string[] ValidManualStatuses = ["Pending", "Cleared"];

    public async Task<CashTransactionPageDto> GetPagedAsync(CashTransactionFilter filter, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(db.CashTransactions.AsNoTracking(), filter);

        var totalCount = await query.CountAsync(cancellationToken);

        // Filter-wide totals; transfer legs move balances but aren't income or spend.
        var totalIn = await query
            .Where(t => t.Direction == "In" && t.TransferGroupId == null)
            .SumAsync(t => t.Amount, cancellationToken);
        var totalOut = await query
            .Where(t => t.Direction == "Out" && t.TransferGroupId == null)
            .SumAsync(t => t.Amount, cancellationToken);

        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);

        var items = await query
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Include(t => t.Invoice)
            .Include(t => t.Attachments)
            .OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAtUtc).ThenByDescending(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(t => ToDto(t)).ToList();

        var runningBalances = await ComputeRunningBalancesAsync(filter, items, cancellationToken);
        if (runningBalances is not null)
        {
            dtos = dtos.Select((dto, i) => dto with { RunningBalance = runningBalances[i] }).ToList();
        }

        return new CashTransactionPageDto(dtos, page, pageSize, totalCount, totalIn, totalOut);
    }

    public async Task<CashTransactionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var transaction = await db.CashTransactions.AsNoTracking()
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Include(t => t.Invoice)
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        return transaction is null ? null : ToDto(transaction);
    }

    public async Task<string> ExportCsvAsync(CashTransactionFilter filter, CancellationToken cancellationToken = default)
    {
        var rows = await ApplyFilter(db.CashTransactions.AsNoTracking(), filter)
            .Include(t => t.Account)
            .Include(t => t.Category)
            .OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAtUtc).ThenByDescending(t => t.Id)
            .ToListAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Date,Account,Direction,Amount,Category,Counterparty,Description,Status,GST Treatment,Type,Notes");
        foreach (var t in rows)
        {
            var cells = new[]
            {
                t.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                t.Account?.Name ?? string.Empty,
                t.Direction,
                t.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                t.Category?.Name ?? string.Empty,
                t.Counterparty ?? string.Empty,
                t.Description,
                t.Status,
                t.GstTreatment,
                ProvenanceOf(t),
                t.Notes ?? string.Empty,
            };
            sb.AppendLine(string.Join(",", cells.Select(CsvEscape)));
        }

        return sb.ToString();
    }

    public async Task<(CashTransactionDto? Transaction, CashTransactionWriteError Error)> CreateAsync(
        CreateCashTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var lockError = await CheckLockAsync(request.Date, cancellationToken);
        if (lockError != CashTransactionWriteError.None)
        {
            return (null, lockError);
        }

        var (context, error) = await ValidateAsync(
            request.AccountId, request.CategoryId, request.PayeeId, request.Direction,
            request.Amount, request.GstTreatment, request.Status, cancellationToken);
        if (error != CashTransactionWriteError.None)
        {
            return (null, error);
        }

        var transaction = new CashTransaction
        {
            AccountId = request.AccountId,
            Direction = request.Direction,
            Amount = request.Amount,
            Date = request.Date,
            Description = request.Description,
            CategoryId = request.CategoryId,
            PayeeId = request.PayeeId,
            Counterparty = context.Payee?.Name ?? request.Counterparty,
            Status = request.Status ?? "Cleared",
            GstTreatment = request.GstTreatment ?? context.Category!.DefaultGstTreatment,
            Notes = request.Notes,
        };

        db.CashTransactions.Add(transaction);
        audit.Record("CashTransaction", transaction.Id, "Created",
            $"Recorded {transaction.Direction} {transaction.Amount:0.00} — {transaction.Description}");
        await db.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(transaction.Id, cancellationToken), CashTransactionWriteError.None);
    }

    public async Task<(CashTransactionDto? Transaction, CashTransactionWriteError Error)> UpdateAsync(
        Guid id, UpdateCashTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var transaction = await db.CashTransactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Include(t => t.Payee)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (transaction is null)
        {
            return (null, CashTransactionWriteError.NotFound);
        }

        var guard = GuardManagedRow(transaction, allowSplitLines: false);
        if (guard != CashTransactionWriteError.None)
        {
            return (null, guard);
        }

        // Both the row's current period and the period it's moving to must be unlocked.
        var lockError = await CheckLockAsync(transaction.Date, cancellationToken);
        if (lockError == CashTransactionWriteError.None)
        {
            lockError = await CheckLockAsync(request.Date, cancellationToken);
        }

        if (lockError != CashTransactionWriteError.None)
        {
            return (null, lockError);
        }

        var (context, error) = await ValidateAsync(
            request.AccountId, request.CategoryId, request.PayeeId, request.Direction,
            request.Amount, request.GstTreatment, request.Status, cancellationToken);
        if (error != CashTransactionWriteError.None)
        {
            return (null, error);
        }

        var newCounterparty = context.Payee?.Name ?? request.Counterparty;
        var newGst = request.GstTreatment ?? context.Category!.DefaultGstTreatment;
        var newStatus = request.Status ?? transaction.Status;

        var changes = new List<AuditFieldChange>();
        AuditDiff.Add(changes, "Account", transaction.Account?.Name, context.Account!.Name);
        AuditDiff.Add(changes, "Direction", transaction.Direction, request.Direction);
        AuditDiff.Add(changes, "Amount", transaction.Amount, request.Amount);
        AuditDiff.Add(changes, "Date", transaction.Date, request.Date);
        AuditDiff.Add(changes, "Description", transaction.Description, request.Description);
        AuditDiff.Add(changes, "Category", transaction.Category?.Name, context.Category!.Name);
        AuditDiff.Add(changes, "Counterparty", transaction.Counterparty, newCounterparty);
        AuditDiff.Add(changes, "Status", transaction.Status, newStatus);
        AuditDiff.Add(changes, "GST", transaction.GstTreatment, newGst);
        AuditDiff.Add(changes, "Notes", transaction.Notes, request.Notes);

        transaction.AccountId = request.AccountId;
        transaction.Direction = request.Direction;
        transaction.Amount = request.Amount;
        transaction.Date = request.Date;
        transaction.Description = request.Description;
        transaction.CategoryId = request.CategoryId;
        transaction.PayeeId = request.PayeeId;
        transaction.Counterparty = newCounterparty;
        transaction.Status = newStatus;
        transaction.GstTreatment = newGst;
        transaction.Notes = request.Notes;

        if (changes.Count > 0)
        {
            audit.Record("CashTransaction", transaction.Id, "Updated", AuditDiff.Summarize(changes), changes);
        }

        await db.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(id, cancellationToken), CashTransactionWriteError.None);
    }

    public async Task<CashTransactionWriteError> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var transaction = await db.CashTransactions
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (transaction is null)
        {
            return CashTransactionWriteError.NotFound;
        }

        if (transaction.InvoiceId is not null)
        {
            return CashTransactionWriteError.InvoiceLinkedReadOnly;
        }

        // A transfer only makes sense as a pair — and a split as a whole entry — so undoing
        // one row undoes the group.
        List<CashTransaction> toRemove;
        if (transaction.TransferGroupId is not null)
        {
            toRemove = await db.CashTransactions
                .Include(t => t.Attachments)
                .Where(t => t.TransferGroupId == transaction.TransferGroupId)
                .ToListAsync(cancellationToken);
        }
        else if (transaction.SplitGroupId is not null)
        {
            toRemove = await db.CashTransactions
                .Include(t => t.Attachments)
                .Where(t => t.SplitGroupId == transaction.SplitGroupId)
                .ToListAsync(cancellationToken);
        }
        else
        {
            toRemove = [transaction];
        }

        if (toRemove.Any(t => t.Status == "Reconciled"))
        {
            return CashTransactionWriteError.ReconciledReadOnly;
        }

        foreach (var row in toRemove)
        {
            var lockError = await CheckLockAsync(row.Date, cancellationToken);
            if (lockError != CashTransactionWriteError.None)
            {
                return lockError;
            }
        }

        foreach (var attachment in toRemove.SelectMany(t => t.Attachments))
        {
            await fileStorage.DeleteAsync(attachment.StorageKey, cancellationToken);
        }

        db.CashTransactions.RemoveRange(toRemove);
        RecordGroupDelete(transaction, toRemove);
        await db.SaveChangesAsync(cancellationToken);

        return CashTransactionWriteError.None;
    }

    public async Task<(IReadOnlyList<CashTransactionDto>? Legs, CashTransactionWriteError Error)> CreateTransferAsync(
        CreateTransferRequest request, CancellationToken cancellationToken = default)
    {
        if (request.FromAccountId == request.ToAccountId)
        {
            return (null, CashTransactionWriteError.SameAccountTransfer);
        }

        if (request.Amount <= 0)
        {
            return (null, CashTransactionWriteError.NonPositiveAmount);
        }

        var lockError = await CheckLockAsync(request.Date, cancellationToken);
        if (lockError != CashTransactionWriteError.None)
        {
            return (null, lockError);
        }

        var accounts = await db.CashAccounts
            .Where(a => a.Id == request.FromAccountId || a.Id == request.ToAccountId)
            .ToListAsync(cancellationToken);
        var from = accounts.FirstOrDefault(a => a.Id == request.FromAccountId);
        var to = accounts.FirstOrDefault(a => a.Id == request.ToAccountId);
        if (from is null || to is null)
        {
            return (null, CashTransactionWriteError.AccountNotFound);
        }

        if (from.IsArchived || to.IsArchived)
        {
            return (null, CashTransactionWriteError.AccountArchived);
        }

        var category = await CashFlowSeeder.EnsureSystemCategoryAsync(db, CashFlowSeeder.TransferCategory, cancellationToken);
        var transferGroupId = Guid.NewGuid();

        var legs = new[]
        {
            NewLeg(from.Id, "Out", request.Description ?? $"Transfer to {to.Name}"),
            NewLeg(to.Id, "In", request.Description ?? $"Transfer from {from.Name}"),
        };

        db.CashTransactions.AddRange(legs);
        audit.Record("CashTransfer", transferGroupId, "Created",
            $"Transferred {request.Amount:0.00} from {from.Name} to {to.Name}");
        await db.SaveChangesAsync(cancellationToken);

        var dtos = new List<CashTransactionDto>();
        foreach (var leg in legs)
        {
            dtos.Add((await GetByIdAsync(leg.Id, cancellationToken))!);
        }

        return (dtos, CashTransactionWriteError.None);

        CashTransaction NewLeg(Guid accountId, string direction, string description) => new()
        {
            AccountId = accountId,
            Direction = direction,
            Amount = request.Amount,
            Date = request.Date,
            Description = description,
            CategoryId = category.Id,
            TransferGroupId = transferGroupId,
            GstTreatment = "Exempt",
            Notes = request.Notes,
        };
    }

    public async Task<(IReadOnlyList<CashTransactionDto>? Lines, CashTransactionWriteError Error)> CreateSplitAsync(
        CreateSplitTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var (context, error) = await ValidateSplitAsync(
            request.AccountId, request.PayeeId, request.Direction, request.Status, request.Date, request.Lines, cancellationToken);
        if (error != CashTransactionWriteError.None)
        {
            return (null, error);
        }

        var splitGroupId = Guid.NewGuid();
        var rows = BuildSplitRows(splitGroupId, request.AccountId, request.Direction, request.Date,
            request.Description, request.PayeeId, context.Payee?.Name ?? request.Counterparty,
            request.Status, request.Notes, request.Lines, context.Categories!);

        db.CashTransactions.AddRange(rows);
        audit.Record("CashSplit", splitGroupId, "Created",
            $"Split {request.Direction} {rows.Sum(r => r.Amount):0.00} across {rows.Count} categories — {request.Description}");
        await db.SaveChangesAsync(cancellationToken);

        return (await LoadGroupAsync(splitGroupId, cancellationToken), CashTransactionWriteError.None);
    }

    public async Task<(IReadOnlyList<CashTransactionDto>? Lines, CashTransactionWriteError Error)> UpdateSplitAsync(
        Guid splitGroupId, UpdateSplitTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await db.CashTransactions
            .Include(t => t.Attachments)
            .Where(t => t.SplitGroupId == splitGroupId)
            .ToListAsync(cancellationToken);
        if (existing.Count == 0)
        {
            return (null, CashTransactionWriteError.NotFound);
        }

        if (existing.Any(t => t.Status == "Reconciled"))
        {
            return (null, CashTransactionWriteError.ReconciledReadOnly);
        }

        foreach (var row in existing)
        {
            var rowLock = await CheckLockAsync(row.Date, cancellationToken);
            if (rowLock != CashTransactionWriteError.None)
            {
                return (null, rowLock);
            }
        }

        var (context, error) = await ValidateSplitAsync(
            request.AccountId, request.PayeeId, request.Direction, request.Status, request.Date, request.Lines, cancellationToken);
        if (error != CashTransactionWriteError.None)
        {
            return (null, error);
        }

        foreach (var attachment in existing.SelectMany(t => t.Attachments))
        {
            await fileStorage.DeleteAsync(attachment.StorageKey, cancellationToken);
        }

        db.CashTransactions.RemoveRange(existing);

        var rows = BuildSplitRows(splitGroupId, request.AccountId, request.Direction, request.Date,
            request.Description, request.PayeeId, context.Payee?.Name ?? request.Counterparty,
            request.Status, request.Notes, request.Lines, context.Categories!);
        db.CashTransactions.AddRange(rows);

        audit.Record("CashSplit", splitGroupId, "Updated",
            $"Split rewritten: {existing.Sum(SignedAmount):0.00} ({existing.Count} lines) → {rows.Sum(SignedAmount):0.00} ({rows.Count} lines)");
        await db.SaveChangesAsync(cancellationToken);

        return (await LoadGroupAsync(splitGroupId, cancellationToken), CashTransactionWriteError.None);
    }

    public async Task<(BulkCashTransactionResultDto? Result, CashTransactionWriteError Error)> BulkAsync(
        BulkCashTransactionRequest request, CancellationToken cancellationToken = default)
    {
        TransactionCategory? category = null;
        switch (request.Action)
        {
            case "SetCategory":
                category = await db.TransactionCategories.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);
                if (category is null)
                {
                    return (null, CashTransactionWriteError.CategoryNotFound);
                }

                break;
            case "SetStatus":
                if (request.Status is null || !ValidManualStatuses.Contains(request.Status))
                {
                    return (null, CashTransactionWriteError.InvalidStatus);
                }

                break;
            case "Delete":
                break;
            default:
                return (null, CashTransactionWriteError.InvalidBulkAction);
        }

        var lockDate = await GetLockDateAsync(cancellationToken);
        var rows = await db.CashTransactions
            .Include(t => t.Attachments)
            .Where(t => request.Ids.Contains(t.Id))
            .ToListAsync(cancellationToken);
        var byId = rows.ToDictionary(t => t.Id);

        var updated = 0;
        var skipped = new List<BulkSkippedRowDto>();
        foreach (var id in request.Ids.Distinct())
        {
            if (!byId.TryGetValue(id, out var t))
            {
                skipped.Add(new BulkSkippedRowDto(id, "Not found"));
                continue;
            }

            var reason = SkipReasonFor(t, request.Action, category, lockDate);
            if (reason is not null)
            {
                skipped.Add(new BulkSkippedRowDto(id, reason));
                continue;
            }

            switch (request.Action)
            {
                case "SetCategory" when t.CategoryId != category!.Id:
                    var categoryChanges = new List<AuditFieldChange>();
                    AuditDiff.Add(categoryChanges, "Category", t.CategoryId, category.Id);
                    audit.Record("CashTransaction", t.Id, "Updated", $"Bulk: category set to {category.Name}", categoryChanges);
                    t.CategoryId = category.Id;
                    updated++;
                    break;
                case "SetStatus" when t.Status != request.Status:
                    var statusChanges = new List<AuditFieldChange>();
                    AuditDiff.Add(statusChanges, "Status", t.Status, request.Status);
                    audit.Record("CashTransaction", t.Id, "Updated", $"Bulk: status set to {request.Status}", statusChanges);
                    t.Status = request.Status!;
                    updated++;
                    break;
                case "Delete":
                    foreach (var attachment in t.Attachments)
                    {
                        await fileStorage.DeleteAsync(attachment.StorageKey, cancellationToken);
                    }

                    audit.Record("CashTransaction", t.Id, "Deleted",
                        $"Bulk: deleted {t.Direction} {t.Amount:0.00} — {t.Description}");
                    db.CashTransactions.Remove(t);
                    updated++;
                    break;
            }
        }

        if (updated > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return (new BulkCashTransactionResultDto(updated, skipped), CashTransactionWriteError.None);
    }

    public async Task<TransactionAttachmentDto?> AddAttachmentAsync(
        Guid transactionId, Stream content, string fileName, string contentType, long sizeBytes, CancellationToken cancellationToken = default)
    {
        var exists = await db.CashTransactions.AnyAsync(t => t.Id == transactionId, cancellationToken);
        if (!exists)
        {
            return null;
        }

        var storageKey = await fileStorage.SaveAsync(content, fileName, cancellationToken);
        var attachment = new TransactionAttachment
        {
            CashTransactionId = transactionId,
            FileName = fileName,
            ContentType = contentType,
            StorageKey = storageKey,
            SizeBytes = sizeBytes,
        };

        db.TransactionAttachments.Add(attachment);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(attachment);
    }

    public async Task<(TransactionAttachmentDto Attachment, Stream Content)?> GetAttachmentAsync(
        Guid transactionId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await db.TransactionAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.CashTransactionId == transactionId, cancellationToken);
        if (attachment is null)
        {
            return null;
        }

        var content = await fileStorage.OpenReadAsync(attachment.StorageKey, cancellationToken);
        return content is null ? null : (ToDto(attachment), content);
    }

    public async Task<bool> DeleteAttachmentAsync(Guid transactionId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await db.TransactionAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.CashTransactionId == transactionId, cancellationToken);
        if (attachment is null)
        {
            return false;
        }

        await fileStorage.DeleteAsync(attachment.StorageKey, cancellationToken);
        db.TransactionAttachments.Remove(attachment);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static IQueryable<CashTransaction> ApplyFilter(IQueryable<CashTransaction> query, CashTransactionFilter filter)
    {
        if (filter.AccountId is not null)
        {
            query = query.Where(t => t.AccountId == filter.AccountId);
        }

        if (filter.CategoryId is not null)
        {
            query = query.Where(t => t.CategoryId == filter.CategoryId);
        }

        if (filter.PayeeId is not null)
        {
            query = query.Where(t => t.PayeeId == filter.PayeeId);
        }

        if (filter.SplitGroupId is not null)
        {
            query = query.Where(t => t.SplitGroupId == filter.SplitGroupId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Direction))
        {
            query = query.Where(t => t.Direction == filter.Direction);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(t => t.Status == filter.Status);
        }

        if (filter.From is not null)
        {
            query = query.Where(t => t.Date >= filter.From);
        }

        if (filter.To is not null)
        {
            query = query.Where(t => t.Date <= filter.To);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            query = query.Where(t =>
                t.Description.ToLower().Contains(term) ||
                (t.Counterparty != null && t.Counterparty.ToLower().Contains(term)) ||
                (t.Notes != null && t.Notes.ToLower().Contains(term)));
        }

        return query;
    }

    /// <summary>
    /// Running balances are only meaningful when the page holds *every* account row in its
    /// date span — a category/payee/direction/status/search filter thins rows out and would
    /// make consecutive-row arithmetic lie. Date filters are fine: they cut a contiguous span.
    /// </summary>
    private async Task<List<decimal>?> ComputeRunningBalancesAsync(
        CashTransactionFilter filter, List<CashTransaction> items, CancellationToken cancellationToken)
    {
        var eligible = filter.AccountId is not null
            && filter.CategoryId is null
            && filter.PayeeId is null
            && filter.SplitGroupId is null
            && string.IsNullOrWhiteSpace(filter.Direction)
            && string.IsNullOrWhiteSpace(filter.Status)
            && string.IsNullOrWhiteSpace(filter.Search);
        if (!eligible || items.Count == 0)
        {
            return null;
        }

        var account = await db.CashAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == filter.AccountId, cancellationToken);
        if (account is null)
        {
            return null;
        }

        // Balance at the newest page row = current balance minus everything strictly newer.
        var newest = items[0];
        var accountRows = db.CashTransactions.AsNoTracking().Where(t => t.AccountId == account.Id);
        var totalSigned = await accountRows.SumAsync(t => t.Direction == "In" ? t.Amount : -t.Amount, cancellationToken);
        var newerSigned = await accountRows
            .Where(t => t.Date > newest.Date
                || (t.Date == newest.Date && t.CreatedAtUtc > newest.CreatedAtUtc))
            .SumAsync(t => t.Direction == "In" ? t.Amount : -t.Amount, cancellationToken);

        var balance = account.OpeningBalance + totalSigned - newerSigned;
        var balances = new List<decimal>(items.Count);
        foreach (var item in items)
        {
            if (!ReferenceEquals(item, newest))
            {
                balance -= SignedAmount(items[balances.Count - 1]);
            }

            balances.Add(balance);
        }

        return balances;
    }

    private async Task<IReadOnlyList<CashTransactionDto>> LoadGroupAsync(Guid splitGroupId, CancellationToken cancellationToken)
    {
        var rows = await db.CashTransactions.AsNoTracking()
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Include(t => t.Invoice)
            .Include(t => t.Attachments)
            .Where(t => t.SplitGroupId == splitGroupId)
            .OrderBy(t => t.CreatedAtUtc).ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);
        return rows.Select(t => ToDto(t)).ToList();
    }

    private static List<CashTransaction> BuildSplitRows(
        Guid splitGroupId, Guid accountId, string direction, DateOnly date, string baseDescription,
        Guid? payeeId, string? counterparty, string? status, string? notes,
        IReadOnlyList<SplitTransactionLine> lines, Dictionary<Guid, TransactionCategory> categories) =>
        lines.Select(line => new CashTransaction
        {
            AccountId = accountId,
            Direction = direction,
            Amount = line.Amount,
            Date = date,
            Description = line.Description ?? baseDescription,
            CategoryId = line.CategoryId,
            PayeeId = payeeId,
            Counterparty = counterparty,
            Status = status ?? "Cleared",
            SplitGroupId = splitGroupId,
            GstTreatment = line.GstTreatment ?? categories[line.CategoryId].DefaultGstTreatment,
            Notes = notes,
        }).ToList();

    private sealed record SplitValidationContext(Payee? Payee, Dictionary<Guid, TransactionCategory>? Categories);

    private async Task<(SplitValidationContext Context, CashTransactionWriteError Error)> ValidateSplitAsync(
        Guid accountId, Guid? payeeId, string direction, string? status, DateOnly date,
        IReadOnlyList<SplitTransactionLine> lines, CancellationToken cancellationToken)
    {
        var empty = new SplitValidationContext(null, null);

        if (!ValidDirections.Contains(direction))
        {
            return (empty, CashTransactionWriteError.InvalidDirection);
        }

        if (status is not null && !ValidManualStatuses.Contains(status))
        {
            return (empty, CashTransactionWriteError.InvalidStatus);
        }

        if (lines is null || lines.Count < 2)
        {
            return (empty, CashTransactionWriteError.SplitNeedsTwoLines);
        }

        if (lines.Any(l => l.Amount <= 0))
        {
            return (empty, CashTransactionWriteError.NonPositiveAmount);
        }

        if (lines.Any(l => l.GstTreatment is not null && !ValidGstTreatments.Contains(l.GstTreatment)))
        {
            return (empty, CashTransactionWriteError.InvalidGstTreatment);
        }

        var lockError = await CheckLockAsync(date, cancellationToken);
        if (lockError != CashTransactionWriteError.None)
        {
            return (empty, lockError);
        }

        var account = await db.CashAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        if (account is null)
        {
            return (empty, CashTransactionWriteError.AccountNotFound);
        }

        if (account.IsArchived)
        {
            return (empty, CashTransactionWriteError.AccountArchived);
        }

        Payee? payee = null;
        if (payeeId is not null)
        {
            payee = await db.Payees.AsNoTracking().FirstOrDefaultAsync(p => p.Id == payeeId, cancellationToken);
            if (payee is null)
            {
                return (empty, CashTransactionWriteError.PayeeNotFound);
            }

            if (payee.IsArchived)
            {
                return (empty, CashTransactionWriteError.PayeeArchived);
            }
        }

        var categoryIds = lines.Select(l => l.CategoryId).Distinct().ToList();
        var categories = await db.TransactionCategories.AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);
        if (categories.Count != categoryIds.Count)
        {
            return (empty, CashTransactionWriteError.CategoryNotFound);
        }

        if (categories.Values.Any(c => c.Direction != "Either" && c.Direction != direction))
        {
            return (empty, CashTransactionWriteError.DirectionMismatchesCategory);
        }

        return (new SplitValidationContext(payee, categories), CashTransactionWriteError.None);
    }

    private sealed record ValidationContext(CashAccount? Account, TransactionCategory? Category, Payee? Payee);

    // Validates the shared create/update rules; returns the looked-up rows so callers can
    // apply defaults (GST from category, counterparty from payee) without extra queries.
    private async Task<(ValidationContext Context, CashTransactionWriteError Error)> ValidateAsync(
        Guid accountId, Guid categoryId, Guid? payeeId, string direction, decimal amount,
        string? gstTreatment, string? status, CancellationToken cancellationToken)
    {
        var empty = new ValidationContext(null, null, null);

        if (!ValidDirections.Contains(direction))
        {
            return (empty, CashTransactionWriteError.InvalidDirection);
        }

        if (amount <= 0)
        {
            return (empty, CashTransactionWriteError.NonPositiveAmount);
        }

        if (gstTreatment is not null && !ValidGstTreatments.Contains(gstTreatment))
        {
            return (empty, CashTransactionWriteError.InvalidGstTreatment);
        }

        if (status is not null && !ValidManualStatuses.Contains(status))
        {
            return (empty, CashTransactionWriteError.InvalidStatus);
        }

        var account = await db.CashAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        if (account is null)
        {
            return (empty, CashTransactionWriteError.AccountNotFound);
        }

        if (account.IsArchived)
        {
            return (empty, CashTransactionWriteError.AccountArchived);
        }

        var category = await db.TransactionCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken);
        if (category is null)
        {
            return (empty, CashTransactionWriteError.CategoryNotFound);
        }

        if (category.Direction != "Either" && category.Direction != direction)
        {
            return (empty, CashTransactionWriteError.DirectionMismatchesCategory);
        }

        Payee? payee = null;
        if (payeeId is not null)
        {
            payee = await db.Payees.AsNoTracking().FirstOrDefaultAsync(p => p.Id == payeeId, cancellationToken);
            if (payee is null)
            {
                return (empty, CashTransactionWriteError.PayeeNotFound);
            }

            if (payee.IsArchived)
            {
                return (empty, CashTransactionWriteError.PayeeArchived);
            }
        }

        return (new ValidationContext(account, category, payee), CashTransactionWriteError.None);
    }

    /// <summary>Read-only guards, in the documented precedence order.</summary>
    private static CashTransactionWriteError GuardManagedRow(CashTransaction t, bool allowSplitLines)
    {
        if (t.InvoiceId is not null)
        {
            return CashTransactionWriteError.InvoiceLinkedReadOnly;
        }

        if (t.TransferGroupId is not null)
        {
            return CashTransactionWriteError.TransferLegReadOnly;
        }

        if (!allowSplitLines && t.SplitGroupId is not null)
        {
            return CashTransactionWriteError.SplitLineReadOnly;
        }

        if (t.Status == "Reconciled")
        {
            return CashTransactionWriteError.ReconciledReadOnly;
        }

        return CashTransactionWriteError.None;
    }

    private static string? SkipReasonFor(CashTransaction t, string action, TransactionCategory? category, DateOnly? lockDate)
    {
        // Status is bank-side state, so it may change on any non-reconciled row; content
        // changes respect the managed-row rules.
        if (t.Status == "Reconciled")
        {
            return "Reconciled rows are immutable";
        }

        if (lockDate is not null && t.Date <= lockDate)
        {
            return "Period is locked";
        }

        switch (action)
        {
            case "SetCategory":
                if (t.InvoiceId is not null)
                {
                    return "Posted from an invoice";
                }

                if (t.TransferGroupId is not null)
                {
                    return "Transfer leg";
                }

                if (category!.Direction != "Either" && category.Direction != t.Direction)
                {
                    return "Category doesn't apply to that direction";
                }

                break;
            case "Delete":
                if (t.InvoiceId is not null)
                {
                    return "Posted from an invoice";
                }

                if (t.TransferGroupId is not null)
                {
                    return "Transfer leg — delete it from the transfer";
                }

                if (t.SplitGroupId is not null)
                {
                    return "Split line — delete the whole split";
                }

                break;
        }

        return null;
    }

    private async Task<CashTransactionWriteError> CheckLockAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var lockDate = await GetLockDateAsync(cancellationToken);
        return lockDate is not null && date <= lockDate
            ? CashTransactionWriteError.PeriodLocked
            : CashTransactionWriteError.None;
    }

    private async Task<DateOnly?> GetLockDateAsync(CancellationToken cancellationToken) =>
        (await db.CashFlowSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken))?.LockDate;

    private void RecordGroupDelete(CashTransaction requested, List<CashTransaction> removed)
    {
        if (requested.TransferGroupId is not null)
        {
            audit.Record("CashTransfer", requested.TransferGroupId.Value, "Deleted",
                $"Transfer of {removed.Max(t => t.Amount):0.00} deleted (both legs)");
        }
        else if (requested.SplitGroupId is not null)
        {
            audit.Record("CashSplit", requested.SplitGroupId.Value, "Deleted",
                $"Split of {removed.Sum(t => t.Amount):0.00} deleted ({removed.Count} lines)");
        }
        else
        {
            audit.Record("CashTransaction", requested.Id, "Deleted",
                $"Deleted {requested.Direction} {requested.Amount:0.00} — {requested.Description}");
        }
    }

    private static decimal SignedAmount(CashTransaction t) => t.Direction == "In" ? t.Amount : -t.Amount;

    private static string ProvenanceOf(CashTransaction t) =>
        t.InvoiceId is not null ? "Invoice"
        : t.TransferGroupId is not null ? "Transfer"
        : t.SplitGroupId is not null ? "Split"
        : t.RecurringTransactionId is not null ? "Recurring"
        : "Manual";

    private static string CsvEscape(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private static CashTransactionDto ToDto(CashTransaction t, decimal? runningBalance = null) =>
        new(t.Id, t.AccountId, t.Account?.Name ?? string.Empty, t.Direction, t.Amount, t.Date, t.Description,
            t.CategoryId, t.Category?.Name ?? string.Empty, t.PayeeId, t.Counterparty, t.Status,
            t.InvoiceId, t.Invoice?.JobId, t.TransferGroupId, t.SplitGroupId,
            t.GstTreatment, t.Notes,
            t.Attachments.OrderBy(a => a.CreatedAtUtc).Select(ToDto).ToList(),
            t.CreatedAtUtc, t.UpdatedAtUtc, runningBalance);

    private static TransactionAttachmentDto ToDto(TransactionAttachment a) =>
        new(a.Id, a.CashTransactionId, a.FileName, a.ContentType, a.SizeBytes, a.CreatedAtUtc);
}
