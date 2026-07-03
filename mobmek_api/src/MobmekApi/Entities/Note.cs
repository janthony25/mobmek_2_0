namespace MobmekApi.Entities;

/// <summary>
/// A freeform sticky note on the system board (e.g. "Order more oil filters",
/// "Need to call this customer"). Undated by design — dated items are <see cref="Reminder"/>s.
/// May optionally point at a <see cref="Customer"/>. Id and audit timestamps come from
/// <see cref="BaseEntity"/>.
/// </summary>
public class Note : BaseEntity
{
    public required string Title { get; set; }

    public string? Body { get; set; }

    /// <summary>Optional date the note is due/relevant, so the board can flag it as due soon.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>Optional colour/label for the sticky on the board.</summary>
    public string? Color { get; set; }

    public bool IsPinned { get; set; }

    public bool IsDone { get; set; }

    /// <summary>When the note was last marked done; cleared when reopened. Lets the
    /// board hide notes that have been done for a while without deleting them.</summary>
    public DateTime? DoneAtUtc { get; set; }

    /// <summary>Optional customer this note relates to (foreign key).</summary>
    public Guid? CustomerId { get; set; }

    public Customer? Customer { get; set; }
}
