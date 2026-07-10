namespace MobmekApi.Entities;

/// <summary>
/// Base type for all persisted entities. Provides an identity and audit timestamps.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Id of the signed-in user whose request last modified this row. Auto-stamped by
    /// <see cref="Data.AppDbContext.SaveChangesAsync(CancellationToken)"/> alongside
    /// <see cref="UpdatedAtUtc"/>; null for rows never touched by a request (seeded/system-only
    /// changes) since there's no signed-in user to attribute those to.</summary>
    public Guid? UpdatedByUserId { get; set; }

    /// <summary>Display name snapshot ("First Last") of <see cref="UpdatedByUserId"/> at the
    /// moment of the update, taken from a claim set at sign-in — not a live join to Employee, so
    /// it can go stale if that employee is later renamed within the same 12h session.</summary>
    public string? UpdatedByName { get; set; }
}
