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

    /// <summary>
    /// Booked by a customer through the public website, awaiting staff approval. It holds
    /// the slot (so nobody else can take it) but is not a commitment until moved to
    /// <see cref="Confirmed"/>.
    /// </summary>
    /// <remarks>
    /// Appended last on purpose. The database stores this enum as a string
    /// (<c>HasConversion&lt;string&gt;</c>), but JSON has no string-enum converter registered,
    /// so the API serializes it as an integer — inserting this at the front in lifecycle
    /// order would renumber every existing status on the wire and silently mislabel every
    /// appointment in the admin UI.
    /// </remarks>
    Requested,
}
