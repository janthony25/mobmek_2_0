namespace MobmekApi.Entities;

/// <summary>
/// A job title an employee can hold (e.g. Manager, Mechanic). Lookup table.
/// Id and audit timestamps come from <see cref="BaseEntity"/>.
/// </summary>
public class EmployeeTitle : BaseEntity
{
    public required string Name { get; set; }

    /// <summary>Employees holding this title.</summary>
    public ICollection<Employee> Employees { get; set; } = [];
}
