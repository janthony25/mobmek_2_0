namespace MobmekApi.Entities;

/// <summary>
/// A Mobmek employee. Id and audit timestamps come from <see cref="BaseEntity"/>.
/// References an <see cref="EmployeeTitle"/> and an <see cref="EmploymentType"/>.
/// </summary>
public class Employee : BaseEntity
{
    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    /// <summary>Job title (foreign key to <see cref="EmployeeTitle"/>).</summary>
    public Guid TitleId { get; set; }

    public EmployeeTitle? Title { get; set; }

    /// <summary>Employment type (foreign key to <see cref="EmploymentType"/>).</summary>
    public Guid EmploymentTypeId { get; set; }

    public EmploymentType? EmploymentType { get; set; }

    public required string ContactNumber { get; set; }

    public required string EmailAddress { get; set; }

    public required string PhysicalAddress { get; set; }
}
