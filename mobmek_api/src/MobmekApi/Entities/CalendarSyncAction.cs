namespace MobmekApi.Entities;

/// <summary>What a <see cref="CalendarSyncItem"/> row asks the sync job to do.</summary>
public enum CalendarSyncAction
{
    /// <summary>Insert (if no event yet) or update the appointment's Google Calendar event.</summary>
    Upsert,

    /// <summary>Delete the snapshotted <see cref="CalendarSyncItem.GoogleEventId"/> from Google Calendar.</summary>
    Delete,
}
