using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface IPayeeService
{
    /// <summary>All payees ordered by name; archived ones only when asked for.</summary>
    Task<IReadOnlyList<PayeeDto>> GetAllAsync(bool includeArchived = false, CancellationToken cancellationToken = default);

    Task<PayeeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Spend history for one payee; <c>null</c> when the payee doesn't exist.</summary>
    Task<PayeeSummaryDto?> GetSummaryAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(PayeeDto? Payee, PayeeWriteError Error)> CreateAsync(CreatePayeeRequest request, CancellationToken cancellationToken = default);

    Task<(PayeeDto? Payee, PayeeWriteError Error)> UpdateAsync(Guid id, UpdatePayeeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Refuses (<see cref="PayeeWriteError.InUse"/>) while transactions or rules reference the payee — archive instead.</summary>
    Task<PayeeWriteError> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
