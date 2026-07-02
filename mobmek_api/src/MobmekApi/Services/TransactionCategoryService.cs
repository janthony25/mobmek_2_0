using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class TransactionCategoryService(AppDbContext db) : ITransactionCategoryService
{
    private static readonly string[] ValidDirections = ["In", "Out", "Either"];
    private static readonly string[] ValidGstTreatments = ["Taxable", "Exempt", "ZeroRated"];

    public async Task<IReadOnlyList<TransactionCategoryDto>> GetAllAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        return await db.TransactionCategories.AsNoTracking()
            .Where(c => includeArchived || !c.IsArchived)
            .OrderBy(c => c.Group).ThenBy(c => c.Name)
            .Select(c => ToDto(c))
            .ToListAsync(cancellationToken);
    }

    public async Task<TransactionCategoryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await db.TransactionCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return category is null ? null : ToDto(category);
    }

    public async Task<(TransactionCategoryDto? Category, TransactionCategoryWriteError Error)> CreateAsync(
        CreateTransactionCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var error = await ValidateAsync(request.Direction, request.DefaultGstTreatment, request.Name, excludeId: null, cancellationToken);
        if (error != TransactionCategoryWriteError.None)
        {
            return (null, error);
        }

        var category = new TransactionCategory
        {
            Name = request.Name,
            Direction = request.Direction,
            Group = request.Group,
            DefaultGstTreatment = request.DefaultGstTreatment ?? "Taxable",
            ExcludeFromOperatingExpense = request.ExcludeFromOperatingExpense,
        };

        db.TransactionCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);

        return (ToDto(category), TransactionCategoryWriteError.None);
    }

    public async Task<(TransactionCategoryDto? Category, TransactionCategoryWriteError Error)> UpdateAsync(
        Guid id, UpdateTransactionCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await db.TransactionCategories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return (null, TransactionCategoryWriteError.NotFound);
        }

        var error = await ValidateAsync(request.Direction, request.DefaultGstTreatment, request.Name, excludeId: id, cancellationToken);
        if (error != TransactionCategoryWriteError.None)
        {
            return (null, error);
        }

        category.Name = request.Name;
        category.IsArchived = request.IsArchived;

        // Auto-posting and reports depend on a system category's direction, group and flags,
        // so those stay fixed; only user categories are fully editable.
        if (!category.IsSystem)
        {
            category.Direction = request.Direction;
            category.Group = request.Group;
            category.DefaultGstTreatment = request.DefaultGstTreatment ?? category.DefaultGstTreatment;
            category.ExcludeFromOperatingExpense = request.ExcludeFromOperatingExpense;
        }

        await db.SaveChangesAsync(cancellationToken);

        return (ToDto(category), TransactionCategoryWriteError.None);
    }

    public async Task<TransactionCategoryWriteError> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await db.TransactionCategories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return TransactionCategoryWriteError.NotFound;
        }

        if (category.IsSystem)
        {
            return TransactionCategoryWriteError.SystemCategory;
        }

        if (await db.CashTransactions.AnyAsync(t => t.CategoryId == id, cancellationToken))
        {
            return TransactionCategoryWriteError.InUse;
        }

        db.TransactionCategories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);

        return TransactionCategoryWriteError.None;
    }

    private async Task<TransactionCategoryWriteError> ValidateAsync(
        string direction, string? gstTreatment, string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (!ValidDirections.Contains(direction))
        {
            return TransactionCategoryWriteError.InvalidDirection;
        }

        if (gstTreatment is not null && !ValidGstTreatments.Contains(gstTreatment))
        {
            return TransactionCategoryWriteError.InvalidGstTreatment;
        }

        if (await db.TransactionCategories.AnyAsync(c => c.Name == name && c.Id != excludeId, cancellationToken))
        {
            return TransactionCategoryWriteError.DuplicateName;
        }

        return TransactionCategoryWriteError.None;
    }

    private static TransactionCategoryDto ToDto(TransactionCategory c) =>
        new(c.Id, c.Name, c.Direction, c.Group, c.IsSystem, c.DefaultGstTreatment,
            c.ExcludeFromOperatingExpense, c.IsArchived, c.CreatedAtUtc, c.UpdatedAtUtc);
}
