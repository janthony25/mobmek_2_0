namespace MobmekApi.Entities;

/// <summary>
/// An auto-categorization rule: when a transaction's text matches, the rule proposes a
/// category (and optionally a GST treatment and payee). Rules power three paths: statement
/// import fills categories before review, the entry form suggests as the user types, and
/// "apply to existing" recategorizes matching history on demand. Lowest
/// <see cref="Priority"/> wins when several rules match.
/// </summary>
public class CategorizationRule : BaseEntity
{
    public required string Name { get; set; }

    /// <summary>Evaluation order; lowest matching rule wins.</summary>
    public int Priority { get; set; }

    /// <summary>"Description", "Counterparty" or "Either" — where <see cref="MatchValue"/> is looked for.</summary>
    public string MatchField { get; set; } = "Either";

    /// <summary>"Contains", "StartsWith" or "Equals" (all case-insensitive).</summary>
    public string MatchType { get; set; } = "Contains";

    public required string MatchValue { get; set; }

    /// <summary>Optional "In"/"Out" restriction; null matches both.</summary>
    public string? Direction { get; set; }

    /// <summary>Optional amount band; either side may be open.</summary>
    public decimal? AmountMin { get; set; }

    public decimal? AmountMax { get; set; }

    /// <summary>The category a match is assigned.</summary>
    public Guid SetCategoryId { get; set; }

    public TransactionCategory? SetCategory { get; set; }

    /// <summary>Optional GST treatment to apply on match.</summary>
    public string? SetGstTreatment { get; set; }

    /// <summary>Optional payee to link on match.</summary>
    public Guid? SetPayeeId { get; set; }

    public Payee? SetPayee { get; set; }

    public bool IsActive { get; set; } = true;
}
