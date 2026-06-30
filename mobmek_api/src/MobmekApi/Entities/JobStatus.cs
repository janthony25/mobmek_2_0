namespace MobmekApi.Entities;

/// <summary>Lifecycle state of a <see cref="Job"/>. Persisted as a string.</summary>
public enum JobStatus
{
    Open,
    InProgress,
    AwaitingParts,
    Completed,
    Invoiced,
}
