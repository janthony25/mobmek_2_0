namespace MobmekApi.Entities;

/// <summary>
/// A template + schedule for a committed regular income/expense (rent, insurance, software
/// subs, loan repayments, retainer income). Occurrences are computed on the fly from
/// <see cref="AnchorDate"/>/<see cref="Frequency"/>/<see cref="Interval"/> — no rows are
/// pre-generated. A materialised occurrence becomes a <see cref="CashTransaction"/> with
/// <see cref="CashTransaction.RecurringTransactionId"/> set, either via the "due — confirm"
/// flow (<see cref="AutoPost"/> = false, the default) or automatically on its date
/// (<see cref="AutoPost"/> = true).
/// </summary>
public class RecurringTransaction : BaseEntity
{
    public required string Description { get; set; }

    /// <summary>"In" or "Out".</summary>
    public required string Direction { get; set; }

    /// <summary>Always positive; GST-inclusive, same convention as <see cref="CashTransaction.Amount"/>.</summary>
    public decimal Amount { get; set; }

    public Guid CategoryId { get; set; }

    public TransactionCategory? Category { get; set; }

    public Guid AccountId { get; set; }

    public CashAccount? Account { get; set; }

    /// <summary>Supplier / payer name.</summary>
    public string? Counterparty { get; set; }

    /// <summary>"Taxable", "Exempt" or "ZeroRated" — stamped onto each materialised occurrence.</summary>
    public string GstTreatment { get; set; } = "Taxable";

    /// <summary>"Weekly", "Fortnightly", "Monthly", "Quarterly" or "Annually".</summary>
    public string Frequency { get; set; } = "Monthly";

    /// <summary>Every N periods (default 1, e.g. Interval=2 + Monthly = every two months).</summary>
    public int Interval { get; set; } = 1;

    /// <summary>The first occurrence; later occurrences step forward from here.</summary>
    public DateOnly AnchorDate { get; set; }

    /// <summary>Open-ended if null.</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>If true, a due occurrence auto-posts a <see cref="CashTransaction"/> on its date via the background job; if false it waits for a one-click confirm (safer default).</summary>
    public bool AutoPost { get; set; }

    /// <summary>Paused schedules produce no further occurrences until resumed.</summary>
    public bool IsPaused { get; set; }
}
