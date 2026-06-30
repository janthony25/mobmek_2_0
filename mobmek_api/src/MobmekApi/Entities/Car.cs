namespace MobmekApi.Entities;

/// <summary>
/// A car owned by a <see cref="Customer"/>. Id and audit timestamps come from <see cref="BaseEntity"/>.
/// </summary>
public class Car : BaseEntity
{
    public required string Make { get; set; }

    public required string Model { get; set; }

    public int Year { get; set; }

    public required string Rego { get; set; }

    public string? Vin { get; set; }

    public string? Color { get; set; }

    public string? EngineType { get; set; }

    public int? Odometer { get; set; }

    /// <summary>Owning customer (foreign key).</summary>
    public Guid CustomerId { get; set; }

    public Customer? Customer { get; set; }
}
