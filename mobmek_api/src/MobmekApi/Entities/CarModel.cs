namespace MobmekApi.Entities;

/// <summary>
/// A model belonging to a <see cref="CarMake"/> (e.g. Z3 under BMW). Lookup table;
/// id and audit timestamps come from <see cref="BaseEntity"/>.
/// </summary>
public class CarModel : BaseEntity
{
    public required string Name { get; set; }

    /// <summary>Owning make (foreign key).</summary>
    public Guid CarMakeId { get; set; }

    public CarMake? CarMake { get; set; }
}
