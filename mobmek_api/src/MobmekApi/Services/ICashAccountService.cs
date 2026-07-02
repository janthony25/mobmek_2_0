using MobmekApi.DTOs;

namespace MobmekApi.Services;

/// <summary>Outcome of a cash-account delete attempt.</summary>
public enum CashAccountDeleteResult
{
    Deleted,
    NotFound,

    /// <summary>The account has ledger history; archive it instead of deleting.</summary>
    HasTransactions,
}

public interface ICashAccountService
{
    /// <summary>Lists accounts (archived ones only when asked) with their derived balances, ordered by name.</summary>
    Task<IReadOnlyList<CashAccountDto>> GetAllAsync(bool includeArchived = false, CancellationToken cancellationToken = default);

    Task<CashAccountDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Sum of the derived balances of all non-archived accounts — the forecast's opening position.</summary>
    Task<decimal> GetTotalBalanceAsync(CancellationToken cancellationToken = default);

    Task<CashAccountDto> CreateAsync(CreateCashAccountRequest request, CancellationToken cancellationToken = default);

    Task<CashAccountDto?> UpdateAsync(Guid id, UpdateCashAccountRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an account that has no transactions (any invoice-payment routes pointing at it
    /// are cleared). An account with ledger history must be archived instead.
    /// </summary>
    Task<CashAccountDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
