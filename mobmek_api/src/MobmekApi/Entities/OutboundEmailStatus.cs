namespace MobmekApi.Entities;

/// <summary>
/// Delivery lifecycle of an <see cref="OutboundEmail"/>. Persisted as a string. Once a row
/// reaches a terminal status (Delivered/Bounced/Complained/Failed) it never regresses —
/// a late-arriving status update cannot overwrite it (see <c>OutboundEmailService.ApplyStatusAsync</c>).
/// </summary>
public enum OutboundEmailStatus
{
    Queued,
    Sent,
    Delivered,
    Bounced,
    Complained,
    Failed,
}
