namespace MobmekApi.DTOs;

/// <summary>Diagnostics for "why isn't my phone showing it" per <c>docs/google-calendar-sync-design.md</c> §6.</summary>
public record CalendarSyncStatusDto(
    bool Configured,
    int OutboxDepth,
    DateTime? OldestPendingUtc,
    DateTime? LastReconcileUtc,
    string? LastReconcileError);
