namespace MobmekApi.Entities;

/// <summary>What triggered an <see cref="OutboundEmail"/> send. Persisted as a string.</summary>
public enum OutboundEmailKind
{
    Invoice,
    Test,
}
