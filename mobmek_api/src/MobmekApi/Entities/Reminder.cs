namespace MobmekApi.Entities;

/// <summary>
/// A dated, actionable reminder for a customer (e.g. "Next Service", "WOF due"), optionally
/// tied to one of their cars and/or created from a <see cref="ReminderTemplate"/>. Id and
/// audit timestamps come from <see cref="BaseEntity"/>.
/// </summary>
public class Reminder : BaseEntity
{
    /// <summary>Customer this reminder is for (foreign key).</summary>
    public Guid CustomerId { get; set; }

    public Customer? Customer { get; set; }

    /// <summary>Optional car this reminder concerns (must belong to <see cref="CustomerId"/>).</summary>
    public Guid? CarId { get; set; }

    public Car? Car { get; set; }

    /// <summary>Optional preset this was created from; the label is copied into <see cref="Title"/>.</summary>
    public Guid? ReminderTemplateId { get; set; }

    public ReminderTemplate? ReminderTemplate { get; set; }

    public required string Title { get; set; }

    /// <summary>Calendar date the reminder is due.</summary>
    public DateOnly DueDate { get; set; }

    public bool IsDone { get; set; }

    public string? Notes { get; set; }
}
