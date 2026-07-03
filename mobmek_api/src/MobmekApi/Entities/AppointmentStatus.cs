namespace MobmekApi.Entities;

/// <summary>Lifecycle state of an <see cref="Appointment"/>. Persisted as a string.</summary>
public enum AppointmentStatus
{
    Scheduled,
    Confirmed,
    Arrived,
    Completed,
    NoShow,
    Cancelled,
}
