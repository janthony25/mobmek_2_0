namespace MobmekApi.Entities;

/// <summary>
/// Singleton row routing invoice payments into cash accounts. When an invoice is marked paid,
/// the cash portion posts to <see cref="CashAccountId"/> and the card / bank-transfer portion
/// to <see cref="CardAccountId"/> / <see cref="BankTransferAccountId"/>, each falling back to
/// <see cref="DefaultAccountId"/>. While no route resolves (nothing configured yet), posting
/// is skipped entirely so invoicing keeps working before the cash-flow module is set up.
/// </summary>
public class CashFlowSettings : BaseEntity
{
    /// <summary>Fallback account for anything without a more specific route.</summary>
    public Guid? DefaultAccountId { get; set; }

    /// <summary>Account the cash portion of an invoice payment posts to (e.g. the till).</summary>
    public Guid? CashAccountId { get; set; }

    /// <summary>Account the card portion of an invoice payment posts to.</summary>
    public Guid? CardAccountId { get; set; }

    /// <summary>Account bank-transfer invoice payments post to.</summary>
    public Guid? BankTransferAccountId { get; set; }

    /// <summary>
    /// Minimum cash the owner wants to keep; the forecast's shortage detection fires the first
    /// projected date the balance drops below this. Defaults to 0 (no buffer) until Phase 3's
    /// <c>TaxProfile</c> takes over as the real home for this setting.
    /// </summary>
    public decimal SafetyBufferAmount { get; set; }

    /// <summary>
    /// Period lock: transactions dated on/before this date can't be created, edited or
    /// deleted (e.g. after handing figures to the accountant). Null = nothing locked.
    /// </summary>
    public DateOnly? LockDate { get; set; }
}
