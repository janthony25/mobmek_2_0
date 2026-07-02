using MobmekApi.DTOs;

namespace MobmekApi.Services;

/// <summary>Why a category write/delete was refused.</summary>
public enum TransactionCategoryWriteError
{
    None,
    NotFound,
    DuplicateName,
    InvalidDirection,
    InvalidGstTreatment,

    /// <summary>System categories are rename/archive-only and can never be deleted.</summary>
    SystemCategory,

    /// <summary>The category still has transactions; archive it instead of deleting.</summary>
    InUse,
}

public interface ITransactionCategoryService
{
    /// <summary>Lists categories (archived ones only when asked), ordered by group then name.</summary>
    Task<IReadOnlyList<TransactionCategoryDto>> GetAllAsync(bool includeArchived = false, CancellationToken cancellationToken = default);

    Task<TransactionCategoryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(TransactionCategoryDto? Category, TransactionCategoryWriteError Error)> CreateAsync(CreateTransactionCategoryRequest request, CancellationToken cancellationToken = default);

    /// <summary>On a system category only the name and archived flag are applied; the rest is fixed.</summary>
    Task<(TransactionCategoryDto? Category, TransactionCategoryWriteError Error)> UpdateAsync(Guid id, UpdateTransactionCategoryRequest request, CancellationToken cancellationToken = default);

    Task<TransactionCategoryWriteError> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
