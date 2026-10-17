namespace MobmekApi.Entities;

/// <summary>
/// A Mobmek customer. Id and audit timestamps come from <see cref="BaseEntity"/>.
/// </summary>
public class Customer : BaseEntity
{
    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    public required string PhoneNumber { get; set; }

    public string? EmailAddress { get; set; }

    public string? PhysicalAddress { get; set; }

    public string? Notes { get; set; }

    /// <summary>Cars owned by this customer.</summary>
    public ICollection<Car> Cars { get; set; } = [];

    /// <summary>Matched/created Xero Contact id, set the first time one of this customer's
    /// invoices syncs (docs/xero-integration-design.md §4) — reused on every later invoice.</summary>
    public string? XeroContactId { get; set; }
}
