namespace MobmekApi.Entities;

/// <summary>How <see cref="JobItem.Markup"/> is applied to the retail price.</summary>
public enum MarkupSolution
{
    /// <summary>Markup is a percentage: SellingPrice = RetailPrice × (1 + Markup/100).</summary>
    Percentage,

    /// <summary>Markup is a flat dollar amount: SellingPrice = RetailPrice + Markup.</summary>
    Dollar,
}

/// <summary>
/// A parts/materials line on a <see cref="Job"/>. The derived money fields
/// (SellingPrice, UnitProfit, ItemTotal) are computed and stored by the backend.
/// </summary>
public class JobItem : BaseEntity
{
    public Guid JobId { get; set; }

    public Job? Job { get; set; }

    public required string ItemName { get; set; }

    /// <summary>What the shop pays (cost). Basis for markup and profit.</summary>
    public decimal? TradePrice { get; set; }

    /// <summary>Reference RRP only; not used in any calculation.</summary>
    public decimal? RetailPrice { get; set; }

    public MarkupSolution MarkupSolution { get; set; }

    public decimal Markup { get; set; }

    public int ItemQuantity { get; set; }

    // --- Computed by the backend (see JobItemService) ---
    public decimal SellingPrice { get; set; }

    public decimal UnitProfit { get; set; }

    public decimal ItemTotal { get; set; }
}
