namespace MobmekApi.Entities;

/// <summary>
/// Singleton configuration row holding the current GST rate applied when generating invoices.
/// Exactly one row is expected; the rate is a fraction (0.15 = 15%) and defaults to 15%.
/// </summary>
public class GstSetting : BaseEntity
{
    /// <summary>GST rate as a fraction. 0.15 means 15%.</summary>
    public decimal Rate { get; set; } = 0.15m;
}
