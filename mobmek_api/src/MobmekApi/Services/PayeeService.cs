using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class PayeeService(AppDbContext db, ICashFlowAuditService audit) : IPayeeService
{
    private static readonly string[] ValidGstTreatments = ["Taxable", "Exempt", "ZeroRated"];

    public async Task<IReadOnlyList<PayeeDto>> GetAllAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var query = db.Payees.AsNoTracking().Include(p => p.DefaultCategory).AsQueryable();
        if (!includeArchived)
        {
            query = query.Where(p => !p.IsArchived);
        }

        var payees = await query.OrderBy(p => p.Name).ToListAsync(cancellationToken);
        return payees.Select(ToDto).ToList();
    }

    public async Task<PayeeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var payee = await db.Payees.AsNoTracking()
            .Include(p => p.DefaultCategory)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return payee is null ? null : ToDto(payee);
    }

    public async Task<PayeeSummaryDto?> GetSummaryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var payee = await db.Payees.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (payee is null)
        {
            return null;
        }

        var rows = db.CashTransactions.AsNoTracking().Where(t => t.PayeeId == id);
        var count = await rows.CountAsync(cancellationToken);
        var firstDate = count == 0 ? (DateOnly?)null : await rows.MinAsync(t => t.Date, cancellationToken);
        var lastDate = count == 0 ? (DateOnly?)null : await rows.MaxAsync(t => t.Date, cancellationToken);

        var windowStart = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-12);
        var totalIn = await rows.Where(t => t.Direction == "In" && t.Date >= windowStart).SumAsync(t => t.Amount, cancellationToken);
        var totalOut = await rows.Where(t => t.Direction == "Out" && t.Date >= windowStart).SumAsync(t => t.Amount, cancellationToken);

        return new PayeeSummaryDto(payee.Id, payee.Name, count, firstDate, lastDate, totalIn, totalOut);
    }

    public async Task<(PayeeDto? Payee, PayeeWriteError Error)> CreateAsync(CreatePayeeRequest request, CancellationToken cancellationToken = default)
    {
        var error = await ValidateAsync(request.Name, request.DefaultCategoryId, request.DefaultGstTreatment, excludeId: null, cancellationToken);
        if (error != PayeeWriteError.None)
        {
            return (null, error);
        }

        var payee = new Payee
        {
            Name = request.Name.Trim(),
            DefaultCategoryId = request.DefaultCategoryId,
            DefaultGstTreatment = request.DefaultGstTreatment,
            Notes = request.Notes,
        };

        db.Payees.Add(payee);
        audit.Record("Payee", payee.Id, "Created", $"Payee \"{payee.Name}\" created");
        await db.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(payee.Id, cancellationToken), PayeeWriteError.None);
    }

    public async Task<(PayeeDto? Payee, PayeeWriteError Error)> UpdateAsync(Guid id, UpdatePayeeRequest request, CancellationToken cancellationToken = default)
    {
        var payee = await db.Payees.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (payee is null)
        {
            return (null, PayeeWriteError.NotFound);
        }

        var error = await ValidateAsync(request.Name, request.DefaultCategoryId, request.DefaultGstTreatment, excludeId: id, cancellationToken);
        if (error != PayeeWriteError.None)
        {
            return (null, error);
        }

        var changes = new List<AuditFieldChange>();
        AuditDiff.Add(changes, "Name", payee.Name, request.Name.Trim());
        AuditDiff.Add(changes, "Default category", payee.DefaultCategoryId, request.DefaultCategoryId);
        AuditDiff.Add(changes, "Default GST", payee.DefaultGstTreatment, request.DefaultGstTreatment);
        AuditDiff.Add(changes, "Notes", payee.Notes, request.Notes);
        AuditDiff.Add(changes, "Archived", payee.IsArchived, request.IsArchived);

        payee.Name = request.Name.Trim();
        payee.DefaultCategoryId = request.DefaultCategoryId;
        payee.DefaultGstTreatment = request.DefaultGstTreatment;
        payee.Notes = request.Notes;
        payee.IsArchived = request.IsArchived;

        if (changes.Count > 0)
        {
            audit.Record("Payee", payee.Id, "Updated", AuditDiff.Summarize(changes), changes);
        }

        await db.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(id, cancellationToken), PayeeWriteError.None);
    }

    public async Task<PayeeWriteError> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var payee = await db.Payees.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (payee is null)
        {
            return PayeeWriteError.NotFound;
        }

        var inUse = await db.CashTransactions.AnyAsync(t => t.PayeeId == id, cancellationToken)
            || await db.CategorizationRules.AnyAsync(r => r.SetPayeeId == id, cancellationToken);
        if (inUse)
        {
            return PayeeWriteError.InUse;
        }

        db.Payees.Remove(payee);
        audit.Record("Payee", payee.Id, "Deleted", $"Payee \"{payee.Name}\" deleted");
        await db.SaveChangesAsync(cancellationToken);
        return PayeeWriteError.None;
    }

    private async Task<PayeeWriteError> ValidateAsync(
        string name, Guid? defaultCategoryId, string? defaultGstTreatment, Guid? excludeId, CancellationToken cancellationToken)
    {
        var trimmed = name.Trim().ToLower();
        var duplicate = await db.Payees.AnyAsync(
            p => p.Name.ToLower() == trimmed && (excludeId == null || p.Id != excludeId), cancellationToken);
        if (duplicate)
        {
            return PayeeWriteError.DuplicateName;
        }

        if (defaultCategoryId is not null
            && !await db.TransactionCategories.AnyAsync(c => c.Id == defaultCategoryId, cancellationToken))
        {
            return PayeeWriteError.CategoryNotFound;
        }

        if (defaultGstTreatment is not null && !ValidGstTreatments.Contains(defaultGstTreatment))
        {
            return PayeeWriteError.InvalidGstTreatment;
        }

        return PayeeWriteError.None;
    }

    private static PayeeDto ToDto(Payee p) =>
        new(p.Id, p.Name, p.DefaultCategoryId, p.DefaultCategory?.Name, p.DefaultGstTreatment,
            p.Notes, p.IsArchived, p.CreatedAtUtc, p.UpdatedAtUtc);
}
