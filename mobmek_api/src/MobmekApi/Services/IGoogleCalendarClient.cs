using Google.Apis.Calendar.v3.Data;

namespace MobmekApi.Services;

/// <summary>
/// Thin wrapper over the Google Calendar API, scoped to the one "Mobmek Workshop" calendar
/// configured via <c>GoogleCalendar:CalendarId</c>. Behind an interface so
/// <see cref="Services.CalendarSyncJob"/> and the enqueue hooks in
/// <see cref="Services.AppointmentService"/> can be tested against a fake (no real Google calls
/// in unit tests). <see cref="IsConfigured"/> is the "unconfigured = cleanly disabled" gate —
/// missing/unreadable credentials or calendar id means every other member is simply never called.
/// </summary>
public interface IGoogleCalendarClient
{
    /// <summary>True once credentials and a calendar id are present and loaded successfully.</summary>
    bool IsConfigured { get; }

    /// <summary>Inserts a new event on the Workshop calendar and returns its id.</summary>
    Task<string> InsertAsync(Event googleEvent, CancellationToken cancellationToken = default);

    /// <summary>Overwrites an existing event's fields.</summary>
    Task UpdateAsync(string eventId, Event googleEvent, CancellationToken cancellationToken = default);

    /// <summary>Deletes an event. A 404/410 (already gone) counts as success, not a failure.</summary>
    Task DeleteAsync(string eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether <paramref name="eventId"/> exists on the Workshop calendar — false for a 404, which
    /// is exactly the case for the 230 legacy ids (they point at the old personal calendar) and
    /// is what tells the sync job to insert a fresh event rather than attempt an update.
    /// </summary>
    Task<bool> EventExistsAsync(string eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists events on the Workshop calendar starting from <paramref name="fromUtc"/>. Reserved for
    /// the Phase 2 reconcile pass — not called by anything in Phase 1.
    /// </summary>
    Task<IReadOnlyList<Event>> ListUpcomingAsync(DateTime fromUtc, CancellationToken cancellationToken = default);
}
