namespace MobmekApi.Entities;

/// <summary>Stable keys for the seeded, non-deletable <see cref="EmailTemplate"/> rows.</summary>
public static class EmailTemplateKeys
{
    public const string InvoiceSend = "InvoiceSend";
    public const string ReminderDue = "ReminderDue";
    public const string AppointmentConfirmation = "AppointmentConfirmation";

    public static readonly string[] All = [InvoiceSend, ReminderDue, AppointmentConfirmation];
}

/// <summary>
/// Editable outbound wording for one kind of email. <see cref="SubjectTemplate"/>/
/// <see cref="BodyIntroTemplate"/> use <c>{{Token}}</c> substitution (see
/// <see cref="Services.EmailTemplateRenderer"/>) — no scripting, unknown tokens render empty.
/// The generated document block (invoice line items/totals, reminder/appointment details,
/// business bank details) is always composed separately in C#; only the subject and the intro
/// paragraph above that block are user-editable.
/// </summary>
public class EmailTemplate : BaseEntity
{
    /// <summary>One of <see cref="EmailTemplateKeys"/>. Seeded rows are never deleted or renamed.</summary>
    public required string Key { get; set; }

    public required string Name { get; set; }

    public required string SubjectTemplate { get; set; }

    public required string BodyIntroTemplate { get; set; }

    /// <summary>Seeded rows: editable wording, fixed key, no delete. Always true in v1 — there is
    /// no UI to create a non-system template.</summary>
    public bool IsSystem { get; set; } = true;
}
