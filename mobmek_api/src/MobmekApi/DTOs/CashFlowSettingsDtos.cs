namespace MobmekApi.DTOs;

/// <summary>
/// Invoice-payment routing: which cash account each portion of a payment posts to.
/// Every route is optional; unset routes fall back to <see cref="DefaultAccountId"/>, and
/// when nothing resolves the payment simply isn't posted to the ledger.
/// </summary>
public record CashFlowSettingsDto(
    Guid Id,
    Guid? DefaultAccountId,
    Guid? CashAccountId,
    Guid? CardAccountId,
    Guid? BankTransferAccountId,
    decimal SafetyBufferAmount,
    DateOnly? LockDate,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record UpdateCashFlowSettingsRequest(
    Guid? DefaultAccountId,
    Guid? CashAccountId,
    Guid? CardAccountId,
    Guid? BankTransferAccountId,
    decimal SafetyBufferAmount,
    DateOnly? LockDate);
