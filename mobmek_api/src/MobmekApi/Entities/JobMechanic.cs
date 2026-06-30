namespace MobmekApi.Entities;

/// <summary>
/// Join row linking a <see cref="Job"/> to a mechanic (<see cref="Employee"/>).
/// A job can have many mechanics; an employee can be on many jobs.
/// </summary>
public class JobMechanic
{
    public Guid JobId { get; set; }

    public Job? Job { get; set; }

    public Guid EmployeeId { get; set; }

    public Employee? Employee { get; set; }
}
