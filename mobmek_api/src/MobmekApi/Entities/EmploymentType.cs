namespace MobmekApi.Entities;

/// <summary>
/// A type of employment (e.g. Full-time, Part-time, Casual, Contract). Lookup table.
/// Id and audit timestamps come from <see cref="BaseEntity"/>.
/// </summary>
public class EmploymentType : BaseEntity
{
    public required string Name { get; set; }

    /// <summary>Employees with this employment type.</summary>
    public ICollection<Employee> Employees { get; set; } = [];
}
