namespace MobmekApi.Entities;

/// <summary>
/// A workshop job for a customer's car. Id and audit timestamps come from <see cref="BaseEntity"/>.
/// Totals are recomputed by the backend from the job's items, labour and services.
/// </summary>
public class Job : BaseEntity
{
    /// <summary>Owning customer (foreign key).</summary>
    public Guid CustomerId { get; set; }

    public Customer? Customer { get; set; }

    /// <summary>Vehicle being worked on (must belong to <see cref="CustomerId"/>).</summary>
    public Guid CarId { get; set; }

    public Car? Car { get; set; }

    public required string Title { get; set; }

    public JobStatus Status { get; set; } = JobStatus.Open;

    public int Odometer { get; set; }

    public string? JobNotes { get; set; }

    public string? InvoiceNotes { get; set; }

    /// <summary>How <see cref="DiscountValue"/> is applied. <see cref="Entities.DiscountType.None"/> means no discount.</summary>
    public DiscountType DiscountType { get; set; } = DiscountType.None;

    /// <summary>A dollar amount (when <see cref="DiscountType"/> is Fixed) or a percentage 0-100 (when Percentage).</summary>
    public decimal DiscountValue { get; set; }

    /// <summary>Total billable amount: items + labour + services, minus the discount.</summary>
    public decimal TotalJobPrice { get; set; }

    /// <summary>Total profit: item profit + labour + services, minus the discount.</summary>
    public decimal TotalJobProfit { get; set; }

    // Children (cascade-deleted with the job).
    public ICollection<JobMechanic> Mechanics { get; set; } = [];

    public ICollection<JobItem> Items { get; set; } = [];

    public ICollection<Labour> Labour { get; set; } = [];

    public ICollection<JobServiceLine> ServiceLines { get; set; } = [];
}
