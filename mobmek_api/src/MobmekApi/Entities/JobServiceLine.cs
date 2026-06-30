namespace MobmekApi.Entities;

/// <summary>
/// A catalog <see cref="JobService"/> attached to a <see cref="Job"/>. The unit price is
/// snapshotted from the catalog when added, so later catalog edits do not change historical
/// jobs. <see cref="LineTotal"/> is computed by the backend.
/// </summary>
public class JobServiceLine : BaseEntity
{
    public Guid JobId { get; set; }

    public Job? Job { get; set; }

    /// <summary>The catalog service this line refers to.</summary>
    public Guid JobServiceId { get; set; }

    public JobService? JobService { get; set; }

    /// <summary>Price captured from the catalog at the time the service was added.</summary>
    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; } = 1;

    // --- Computed by the backend (UnitPrice × Quantity) ---
    public decimal LineTotal { get; set; }
}
