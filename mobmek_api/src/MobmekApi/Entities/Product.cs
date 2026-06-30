namespace MobmekApi.Entities;

/// <summary>
/// Sample domain entity demonstrating the EF Core + service + controller flow.
/// Replace or extend with the real Mobmek domain model.
/// </summary>
public class Product : BaseEntity
{
    public required string Name { get; set; }

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int StockQuantity { get; set; }
}
