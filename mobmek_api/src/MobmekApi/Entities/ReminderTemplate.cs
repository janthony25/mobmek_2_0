namespace MobmekApi.Entities;

/// <summary>
/// A reusable reminder preset (e.g. "Next WOF", "Next Service") the user defines once
/// and picks from when adding a <see cref="Reminder"/>. Lookup table. Id and audit
/// timestamps come from <see cref="BaseEntity"/>.
/// </summary>
public class ReminderTemplate : BaseEntity
{
    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Optional default gap, in months, used by the UI to pre-fill a reminder's due date
    /// (e.g. WOF = 12). Stored only; the backend never computes dates from it.
    /// </summary>
    public int? DefaultIntervalMonths { get; set; }
}
