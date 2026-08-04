namespace MobmekApi.Services;

/// <summary>
/// Ephemeral, process-lifetime diagnostics for the reconcile pass — "why isn't my phone showing
/// it" per <c>docs/google-calendar-sync-design.md</c> §6. Deliberately not persisted (resets on
/// restart): this is a health signal, not business data, matching the "no settings entity in v1"
/// choice already made for the calendar config itself. Registered as a singleton, written by
/// <see cref="CalendarSyncJob"/>, read by the status endpoint.
/// </summary>
public class CalendarSyncStatus
{
    private readonly Lock _lock = new();
    private DateTime? _lastReconcileUtc;
    private string? _lastReconcileError;

    public void RecordSuccess(DateTime atUtc)
    {
        lock (_lock)
        {
            _lastReconcileUtc = atUtc;
            _lastReconcileError = null;
        }
    }

    public void RecordFailure(DateTime atUtc, string error)
    {
        lock (_lock)
        {
            _lastReconcileUtc = atUtc;
            _lastReconcileError = error;
        }
    }

    public (DateTime? LastReconcileUtc, string? LastReconcileError) Snapshot()
    {
        lock (_lock)
        {
            return (_lastReconcileUtc, _lastReconcileError);
        }
    }
}
