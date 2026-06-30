namespace MobmekApi.Entities;

/// <summary>
/// A sellable service in the catalog (e.g. "Oil change", "General service").
/// Id and audit timestamps come from <see cref="BaseEntity"/>. Attached to jobs
/// via <see cref="JobServiceLine"/>.
/// </summary>
public class JobService : BaseEntity
{
    public required string Name { get; set; }

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;
}
