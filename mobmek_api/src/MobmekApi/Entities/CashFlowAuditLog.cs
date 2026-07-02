namespace MobmekApi.Entities;

/// <summary>
/// One audit-trail entry for a mutation of financial data (transactions, transfers, splits,
/// payees, rules, cash-flow settings). Written in the same SaveChanges as the mutation it
/// records, so the trail can never miss a committed change. <see cref="Changes"/> holds a
/// JSON array of <c>{field, old, new}</c> for updates; <see cref="Summary"/> is the
/// human-readable one-liner shown in history views. CreatedAtUtc is the event timestamp.
/// Single-user system today, so there is no actor column yet.
/// </summary>
public class CashFlowAuditLog : BaseEntity
{
    /// <summary>e.g. "CashTransaction", "Payee", "CategorizationRule", "CashFlowSettings".</summary>
    public required string EntityType { get; set; }

    public Guid EntityId { get; set; }

    /// <summary>"Created", "Updated" or "Deleted".</summary>
    public required string Action { get; set; }

    /// <summary>Human-readable one-liner, e.g. "Amount 120.00 → 150.00; Category Fuel → Parts".</summary>
    public required string Summary { get; set; }

    /// <summary>JSON array of {field, old, new}; null for creates/deletes.</summary>
    public string? Changes { get; set; }
}
