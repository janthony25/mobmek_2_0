namespace MobmekApi.Entities;

/// <summary>
/// A booked visit in the workshop calendar. An appointment is a promise, not a record:
/// it can be fully linked to an existing customer/car/job, or carry only free-text
/// contact details (a new caller) that get converted into real Customer/Car records
/// when the vehicle arrives. Id and audit timestamps come from <see cref="BaseEntity"/>.
/// </summary>
public class Appointment : BaseEntity
{
    /// <summary>Reason for the visit, e.g. "Brake inspection".</summary>
    public required string Title { get; set; }

    public DateTime StartUtc { get; set; }

    /// <summary>Must be after <see cref="StartUtc"/>.</summary>
    public DateTime EndUtc { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;

    public string? Notes { get; set; }

    // Soft contact details, taken over the phone. Required (name + phone) when no
    // customer is linked; kept as a snapshot of the original call even after linking.
    public string? ContactName { get; set; }

    public string? ContactPhone { get; set; }

    /// <summary>Free-text vehicle description, e.g. "White 2014 Hilux, rego ABC123".</summary>
    public string? VehicleDescription { get; set; }

    // Hard links, all optional. Filled at booking for existing customers, or at
    // check-in when a new caller's details are converted into real records.
    public Guid? CustomerId { get; set; }

    public Customer? Customer { get; set; }

    /// <summary>Must belong to <see cref="CustomerId"/> when both are set.</summary>
    public Guid? CarId { get; set; }

    public Car? Car { get; set; }

    /// <summary>Job created from (or booked against) this appointment.</summary>
    public Guid? JobId { get; set; }

    public Job? Job { get; set; }

    /// <summary>Assigned mechanic (employee), if known at booking.</summary>
    public Guid? MechanicId { get; set; }

    public Employee? Mechanic { get; set; }

    /// <summary>Google Calendar event id once synced; null until the integration lands.</summary>
    public string? GoogleEventId { get; set; }
}
