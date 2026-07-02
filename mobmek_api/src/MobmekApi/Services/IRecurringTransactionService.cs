using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface IRecurringTransactionService
{
    Task<IReadOnlyList<RecurringTransactionDto>> GetAllAsync(bool includePaused = true, CancellationToken cancellationToken = default);

    Task<RecurringTransactionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(RecurringTransactionDto? Recurring, RecurringTransactionWriteError Error)> CreateAsync(
        CreateRecurringTransactionRequest request, CancellationToken cancellationToken = default);

    Task<(RecurringTransactionDto? Recurring, RecurringTransactionWriteError Error)> UpdateAsync(
        Guid id, UpdateRecurringTransactionRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<RecurringTransactionDto?> SetPausedAsync(Guid id, bool paused, CancellationToken cancellationToken = default);

    /// <summary>Occurrences on or before <paramref name="asOfDate"/> that haven't been posted yet, for active (non-paused, not yet ended) schedules.</summary>
    Task<IReadOnlyList<DueOccurrenceDto>> GetDueOccurrencesAsync(
        DateOnly asOfDate, bool autoPostOnly = false, CancellationToken cancellationToken = default);

    /// <summary>Materialises the occurrence on <paramref name="date"/> as a <see cref="Entities.CashTransaction"/>. Refuses if the date isn't a real occurrence of the schedule or was already posted.</summary>
    Task<(CashTransactionDto? Transaction, RecurringTransactionWriteError Error)> PostOccurrenceAsync(
        Guid id, DateOnly date, CancellationToken cancellationToken = default);
}
