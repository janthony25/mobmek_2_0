namespace MobmekApi.Entities;

/// <summary>
/// A normalized counterparty ("Z Energy", "Repco", "IRD") that transactions can link to.
/// Picking a payee pre-fills its default category/GST treatment on the transaction form,
/// and reporting can aggregate spend per payee. Transactions keep their own
/// <see cref="CashTransaction.Counterparty"/> display string (copied from the payee on link)
/// so history survives a payee rename. Payees with history are archived, not deleted.
/// </summary>
public class Payee : BaseEntity
{
    /// <summary>Unique (case-insensitive).</summary>
    public required string Name { get; set; }

    /// <summary>Category pre-filled when this payee is picked on a transaction.</summary>
    public Guid? DefaultCategoryId { get; set; }

    public TransactionCategory? DefaultCategory { get; set; }

    /// <summary>"Taxable", "Exempt" or "ZeroRated"; pre-filled when this payee is picked.</summary>
    public string? DefaultGstTreatment { get; set; }

    public string? Notes { get; set; }

    /// <summary>Archived payees are hidden from pickers but kept so history stays intact.</summary>
    public bool IsArchived { get; set; }
}
