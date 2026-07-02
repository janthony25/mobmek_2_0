namespace MobmekApi.Entities;

/// <summary>
/// A category for classifying cash transactions. A starter NZ-flavoured set is seeded with
/// <see cref="IsSystem"/> = true; system categories can be renamed but never deleted, because
/// invoice auto-posting and transfers resolve them by name and reports rely on their flags.
/// </summary>
public class TransactionCategory : BaseEntity
{
    public required string Name { get; set; }

    /// <summary>Which side of the ledger this category applies to: "In", "Out" or "Either".</summary>
    public string Direction { get; set; } = "Either";

    /// <summary>One-level rollup used by reports (e.g. "Operating", "Payroll", "Taxes", "Financing").</summary>
    public string Group { get; set; } = "Operating";

    /// <summary>Seeded rows are system categories: rename-only, never deletable.</summary>
    public bool IsSystem { get; set; }

    /// <summary>Default GST treatment stamped on new transactions in this category: "Taxable", "Exempt" or "ZeroRated".</summary>
    public string DefaultGstTreatment { get; set; } = "Taxable";

    /// <summary>
    /// True for tax remittances and financing movements (GST/PAYE payments, loan repayments,
    /// drawings, transfers) so burn-rate and operating-expense figures aren't distorted by them.
    /// </summary>
    public bool ExcludeFromOperatingExpense { get; set; }

    public bool IsArchived { get; set; }
}
