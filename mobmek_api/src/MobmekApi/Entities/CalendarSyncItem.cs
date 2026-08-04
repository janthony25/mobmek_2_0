namespace MobmekApi.Entities;

/// <summary>
/// Durable outbox row: one pending push to Google Calendar. Rows are deleted on success, so
/// an empty table means the mirror is fully caught up. No FK to <see cref="Appointment"/> —
/// the row must outlive a hard-deleted appointment (see <see cref="CalendarSyncAction.Delete"/>,
/// which snapshots <see cref="GoogleEventId"/> at delete time since the appointment row is gone
/// by the time the job runs). At most one pending <see cref="CalendarSyncAction.Upsert"/> row
/// per <see cref="AppointmentId"/> (enforced by a unique partial index in
/// <see cref="Data.AppDbContext"/>) — re-editing an appointment before its push runs just
/// leaves the existing row, since the job reads current appointment state at push time.
/// Id and <c>CreatedAtUtc</c> (from <see cref="BaseEntity"/>) double as "enqueued at".
/// </summary>
public class CalendarSyncItem : BaseEntity
{
    public CalendarSyncAction Action { get; set; }

    /// <summary>Set for <see cref="CalendarSyncAction.Upsert"/>; null for <see cref="CalendarSyncAction.Delete"/>.</summary>
    public Guid? AppointmentId { get; set; }

    /// <summary>Snapshotted event id to delete, set for <see cref="CalendarSyncAction.Delete"/>.</summary>
    public string? GoogleEventId { get; set; }

    public int Attempts { get; set; }

    /// <summary>Due when &lt;= now. Backoff on failure: 1 min → 5 min → 30 min → hourly.</summary>
    public DateTime NextAttemptUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Most recent failure message, for diagnostics.</summary>
    public string? LastError { get; set; }
}
