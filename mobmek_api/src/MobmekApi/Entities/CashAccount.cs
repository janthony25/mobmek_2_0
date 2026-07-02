namespace MobmekApi.Entities;

/// <summary>
/// A place the business keeps money — a bank account, physical till/cash, or digital wallet.
/// The current balance is never stored: it is always derived as
/// <see cref="OpeningBalance"/> + inflows − outflows, so it cannot drift out of sync
/// with the ledger. Accounts with transactions are archived rather than deleted.
/// </summary>
public class CashAccount : BaseEntity
{
    public required string Name { get; set; }

    /// <summary>"Bank", "Cash", "DigitalWallet" or "CreditCard".</summary>
    public string Type { get; set; } = "Bank";

    /// <summary>Display-only (shown in the UI; never used to move money).</summary>
    public string? AccountNumber { get; set; }

    /// <summary>Known balance the ledger starts from at <see cref="OpeningDate"/>.</summary>
    public decimal OpeningBalance { get; set; }

    /// <summary>The date <see cref="OpeningBalance"/> was taken; the ledger for this account starts here.</summary>
    public DateOnly OpeningDate { get; set; }

    /// <summary>Archived accounts are hidden from pickers but kept so their history stays intact.</summary>
    public bool IsArchived { get; set; }

    public ICollection<CashTransaction> Transactions { get; set; } = [];
}
