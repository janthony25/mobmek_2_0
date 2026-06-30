namespace MobmekApi.Entities;

/// <summary>
/// A car manufacturer (e.g. BMW, Toyota). Lookup table; id and audit timestamps
/// come from <see cref="BaseEntity"/>. Has many <see cref="CarModel"/>s.
/// </summary>
public class CarMake : BaseEntity
{
    public required string Name { get; set; }

    public ICollection<CarModel> Models { get; set; } = [];
}
