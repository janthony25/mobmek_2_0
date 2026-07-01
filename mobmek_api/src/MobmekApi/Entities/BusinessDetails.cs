namespace MobmekApi.Entities;

/// <summary>
/// Singleton row holding the workshop's letterhead details, shown on generated invoices.
/// Exactly one row is expected; created on first use with a default name.
/// </summary>
public class BusinessDetails : BaseEntity
{
    public string Name { get; set; } = "Mobmek Workshop";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Abn { get; set; }
}
