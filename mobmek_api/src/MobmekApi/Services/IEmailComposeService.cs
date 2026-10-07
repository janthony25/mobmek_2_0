namespace MobmekApi.Services;

/// <summary>A composed invoice email, ready to send (or edit further). Default recipient fields
/// come from the customer on file and may be null (e.g. no email on file), which callers must
/// handle explicitly.</summary>
public record InvoiceEmailDraft(Guid? CustomerId, string? DefaultToAddress, string? DefaultToName, string Subject, string BodyHtml);

/// <summary>A composed reminder email, ready to send. See <see cref="InvoiceEmailDraft"/> for the
/// recipient-field contract.</summary>
public record ReminderEmailDraft(Guid? CustomerId, string? DefaultToAddress, string? DefaultToName, string Subject, string BodyHtml);

/// <summary>A composed appointment-confirmation email, ready to send. The recipient defaults to
/// the linked customer's email, falling back to the soft-contact <c>ContactEmail</c> snapshot
/// for an appointment not yet converted to a real customer.</summary>
public record AppointmentEmailDraft(Guid? CustomerId, string? DefaultToAddress, string? DefaultToName, string Subject, string BodyHtml);

/// <summary>Builds the email subject/body for a generated invoice, a reminder, or an appointment
/// confirmation. A pure function of its inputs (entity + business letterhead + the relevant
/// <see cref="Entities.EmailTemplate"/>), so it's easy to unit test independently of sending.</summary>
public interface IEmailComposeService
{
    /// <summary>Null when the job/invoice combination doesn't exist.</summary>
    Task<InvoiceEmailDraft?> ComposeInvoiceEmailAsync(
        Guid jobId, Guid invoiceId, string? customIntro, CancellationToken cancellationToken = default);

    /// <summary>Null when the reminder doesn't exist.</summary>
    Task<ReminderEmailDraft?> ComposeReminderEmailAsync(
        Guid reminderId, string? customIntro, CancellationToken cancellationToken = default);

    /// <summary>Null when the appointment doesn't exist.</summary>
    Task<AppointmentEmailDraft?> ComposeAppointmentEmailAsync(
        Guid appointmentId, string? customIntro, CancellationToken cancellationToken = default);
}
