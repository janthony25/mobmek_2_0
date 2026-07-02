using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface IPlannedTransactionService
{
    Task<IReadOnlyList<PlannedTransactionDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<PlannedTransactionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(PlannedTransactionDto? Planned, PlannedTransactionWriteError Error)> CreateAsync(
        CreatePlannedTransactionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Only <c>Planned</c> rows are editable; <c>Posted</c>/<c>Cancelled</c> are terminal.</summary>
    Task<(PlannedTransactionDto? Planned, PlannedTransactionWriteError Error)> UpdateAsync(
        Guid id, UpdatePlannedTransactionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Only <c>Planned</c> rows can be deleted.</summary>
    Task<PlannedTransactionWriteError> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
