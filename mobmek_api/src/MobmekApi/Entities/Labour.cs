namespace MobmekApi.Entities;

/// <summary>
/// A labour line on a <see cref="Job"/>. Either hours × rate, or a flat fixed amount.
/// <see cref="TotalAmount"/> is computed and stored by the backend.
/// </summary>
public class Labour : BaseEntity
{
    public Guid JobId { get; set; }

    public Job? Job { get; set; }

    public decimal? Hours { get; set; }

    public decimal? RatePerHour { get; set; }

    /// <summary>When set, overrides hours × rate and becomes the total.</summary>
    public decimal? FixedAmount { get; set; }

    // --- Computed by the backend (see LabourService) ---
    public decimal TotalAmount { get; set; }
}
