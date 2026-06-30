namespace MobmekApi.Entities;

/// <summary>
/// A car owned by a <see cref="Customer"/>. Make/model reference the
/// <see cref="CarMake"/>/<see cref="CarModel"/> lookups. Id and audit timestamps
/// come from <see cref="BaseEntity"/>.
/// </summary>
public class Car : BaseEntity
{
    /// <summary>Manufacturer (foreign key to <see cref="CarMake"/>).</summary>
    public Guid CarMakeId { get; set; }

    public CarMake? CarMake { get; set; }

    /// <summary>Model (foreign key to <see cref="CarModel"/>; must belong to <see cref="CarMakeId"/>).</summary>
    public Guid CarModelId { get; set; }

    public CarModel? CarModel { get; set; }

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
