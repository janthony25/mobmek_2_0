using Google.Apis.Calendar.v3.Data;
using MobmekApi.Services;

namespace MobmekApi.Tests.Fakes;

/// <summary>
/// Scriptable <see cref="IGoogleCalendarClient"/> test double — no mocking library is used in
/// this repo. Defaults to <see cref="IsConfigured"/> = false (matching "no service account yet"),
/// so existing tests that construct <c>AppointmentService</c> without caring about calendar sync
/// keep behaving exactly as before. Records every call for assertions; <see cref="ExistingEventIds"/>
/// lets a test script which event ids the fake considers to already exist on the calendar (the
/// "known id" vs. "legacy foreign id" distinction from the design doc).
/// </summary>
public class FakeGoogleCalendarClient : IGoogleCalendarClient
{
    private readonly Queue<Exception?> _insertFailures = new();
    private readonly Queue<Exception?> _updateFailures = new();
    private readonly Queue<Exception?> _deleteFailures = new();

    public bool IsConfigured { get; set; }

    public HashSet<string> ExistingEventIds { get; } = [];

    public List<Event> InsertedEvents { get; } = [];

    public List<(string EventId, Event Event)> UpdatedEvents { get; } = [];

    public List<string> DeletedEventIds { get; } = [];

    /// <summary>Seeded directly by a test to script what <see cref="ListUpcomingAsync"/> returns
    /// for reconcile scenarios — independent of <see cref="ExistingEventIds"/>, which only drives
    /// the insert-vs-update check.</summary>
    public List<Event> CalendarEvents { get; } = [];

    public void EnqueueInsertFailure(Exception ex) => _insertFailures.Enqueue(ex);

    public void EnqueueUpdateFailure(Exception ex) => _updateFailures.Enqueue(ex);

    public void EnqueueDeleteFailure(Exception ex) => _deleteFailures.Enqueue(ex);

    public Task<string> InsertAsync(Event googleEvent, CancellationToken cancellationToken = default)
    {
        if (_insertFailures.Count > 0 && _insertFailures.Dequeue() is { } ex)
        {
            throw ex;
        }

        InsertedEvents.Add(googleEvent);
        var id = $"fake-event-{Guid.NewGuid():N}";
        ExistingEventIds.Add(id);
        return Task.FromResult(id);
    }

    public Task UpdateAsync(string eventId, Event googleEvent, CancellationToken cancellationToken = default)
    {
        if (_updateFailures.Count > 0 && _updateFailures.Dequeue() is { } ex)
        {
            throw ex;
        }

        UpdatedEvents.Add((eventId, googleEvent));
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string eventId, CancellationToken cancellationToken = default)
    {
        if (_deleteFailures.Count > 0 && _deleteFailures.Dequeue() is { } ex)
        {
            throw ex;
        }

        DeletedEventIds.Add(eventId);
        ExistingEventIds.Remove(eventId);
        return Task.CompletedTask;
    }

    public Task<bool> EventExistsAsync(string eventId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ExistingEventIds.Contains(eventId));

    public Task<IReadOnlyList<Event>> ListUpcomingAsync(DateTime fromUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Event>>(CalendarEvents);
}
