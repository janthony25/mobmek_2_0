using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class CashTransactionService(AppDbContext db, IFileStorage fileStorage) : ICashTransactionService
{
    private static readonly string[] ValidDirections = ["In", "Out"];
    private static readonly string[] ValidGstTreatments = ["Taxable", "Exempt", "ZeroRated"];

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
            .Include(t => t.Attachments)
            .OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new CashTransactionPageDto(items.Select(ToDto).ToList(), page, pageSize, totalCount, totalIn, totalOut);
    }

    public async Task<CashTransactionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var transaction = await db.CashTransactions.AsNoTracking()
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        return transaction is null ? null : ToDto(transaction);
    }

    public async Task<(CashTransactionDto? Transaction, CashTransactionWriteError Error)> CreateAsync(
        CreateCashTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var (category, error) = await ValidateAsync(
            request.AccountId, request.CategoryId, request.Direction, request.Amount, request.GstTreatment, cancellationToken);
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
            Counterparty = request.Counterparty,
            GstTreatment = request.GstTreatment ?? category!.DefaultGstTreatment,
            Notes = request.Notes,
        };

        db.CashTransactions.Add(transaction);
        await db.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(transaction.Id, cancellationToken), CashTransactionWriteError.None);
    }

    public async Task<(CashTransactionDto? Transaction, CashTransactionWriteError Error)> UpdateAsync(
        Guid id, UpdateCashTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var transaction = await db.CashTransactions.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (transaction is null)
        {
            return (null, CashTransactionWriteError.NotFound);
        }

        if (transaction.InvoiceId is not null)
        {
            return (null, CashTransactionWriteError.InvoiceLinkedReadOnly);
        }

        if (transaction.TransferGroupId is not null)
        {
            return (null, CashTransactionWriteError.TransferLegReadOnly);
        }

        var (category, error) = await ValidateAsync(
            request.AccountId, request.CategoryId, request.Direction, request.Amount, request.GstTreatment, cancellationToken);
        if (error != CashTransactionWriteError.None)
        {
            return (null, error);
        }

        transaction.AccountId = request.AccountId;
        transaction.Direction = request.Direction;
        transaction.Amount = request.Amount;
        transaction.Date = request.Date;
        transaction.Description = request.Description;
        transaction.CategoryId = request.CategoryId;
        transaction.Counterparty = request.Counterparty;
        transaction.GstTreatment = request.GstTreatment ?? category!.DefaultGstTreatment;
        transaction.Notes = request.Notes;
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

        // A transfer only makes sense as a pair, so undoing one leg undoes both.
        var toRemove = transaction.TransferGroupId is null
            ? [transaction]
            : await db.CashTransactions
                .Include(t => t.Attachments)
                .Where(t => t.TransferGroupId == transaction.TransferGroupId)
                .ToListAsync(cancellationToken);

        foreach (var attachment in toRemove.SelectMany(t => t.Attachments))
        {
            await fileStorage.DeleteAsync(attachment.StorageKey, cancellationToken);
        }

        db.CashTransactions.RemoveRange(toRemove);
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

        if (!string.IsNullOrWhiteSpace(filter.Direction))
        {
            query = query.Where(t => t.Direction == filter.Direction);
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

    // Validates the shared create/update rules; returns the category so the caller can
    // apply its default GST treatment without a second lookup.
    private async Task<(TransactionCategory? Category, CashTransactionWriteError Error)> ValidateAsync(
        Guid accountId, Guid categoryId, string direction, decimal amount, string? gstTreatment, CancellationToken cancellationToken)
    {
        if (!ValidDirections.Contains(direction))
        {
            return (null, CashTransactionWriteError.InvalidDirection);
        }

        if (amount <= 0)
        {
            return (null, CashTransactionWriteError.NonPositiveAmount);
        }

        if (gstTreatment is not null && !ValidGstTreatments.Contains(gstTreatment))
        {
            return (null, CashTransactionWriteError.InvalidGstTreatment);
        }

        var account = await db.CashAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        if (account is null)
        {
            return (null, CashTransactionWriteError.AccountNotFound);
        }

        if (account.IsArchived)
        {
            return (null, CashTransactionWriteError.AccountArchived);
        }

        var category = await db.TransactionCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken);
        if (category is null)
        {
            return (null, CashTransactionWriteError.CategoryNotFound);
        }

        if (category.Direction != "Either" && category.Direction != direction)
        {
            return (null, CashTransactionWriteError.DirectionMismatchesCategory);
        }

        return (category, CashTransactionWriteError.None);
    }

    private static CashTransactionDto ToDto(CashTransaction t) =>
        new(t.Id, t.AccountId, t.Account?.Name ?? string.Empty, t.Direction, t.Amount, t.Date, t.Description,
            t.CategoryId, t.Category?.Name ?? string.Empty, t.Counterparty, t.InvoiceId, t.TransferGroupId,
            t.GstTreatment, t.Notes,
            t.Attachments.OrderBy(a => a.CreatedAtUtc).Select(ToDto).ToList(),
            t.CreatedAtUtc, t.UpdatedAtUtc);

    private static TransactionAttachmentDto ToDto(TransactionAttachment a) =>
        new(a.Id, a.CashTransactionId, a.FileName, a.ContentType, a.SizeBytes, a.CreatedAtUtc);
}
