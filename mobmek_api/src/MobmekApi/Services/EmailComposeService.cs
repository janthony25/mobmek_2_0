using System.Globalization;
using System.Net;
using System.Text;
using MobmekApi.Data;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class EmailComposeService(AppDbContext db, IBusinessDetailsService businessDetailsService, IEmailTemplateService emailTemplateService) : IEmailComposeService
{
    public async Task<InvoiceEmailDraft?> ComposeInvoiceEmailAsync(
        Guid jobId, Guid invoiceId, string? customIntro, CancellationToken cancellationToken = default)
    {
        var invoice = await db.Invoices.AsNoTracking()
            .Include(i => i.Items)
            .Include(i => i.Job).ThenInclude(j => j!.Customer)
            .Include(i => i.Job).ThenInclude(j => j!.Car).ThenInclude(c => c!.CarMake)
            .Include(i => i.Job).ThenInclude(j => j!.Car).ThenInclude(c => c!.CarModel)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.JobId == jobId, cancellationToken);

        if (invoice is null)
        {
            return null;
        }

        var business = await businessDetailsService.GetCurrentAsync(cancellationToken);
        var customer = invoice.Job?.Customer;
        var documentLabel = invoice.DocumentType == "Quotation" ? "Quotation" : "Invoice";
        var documentNumber = $"{(invoice.DocumentType == "Quotation" ? business.QuotePrefix : business.InvoicePrefix)}-{invoice.SequenceNumber:D4}";

        var template = await emailTemplateService.GetByKeyAsync(EmailTemplateKeys.InvoiceSend, cancellationToken);
        var tokens = new Dictionary<string, string?>
        {
            ["BusinessName"] = business.Name,
            ["CustomerName"] = customer is null ? null : $"{customer.FirstName} {customer.LastName}",
            ["DocumentLabel"] = documentLabel,
            ["DocumentNumber"] = documentNumber,
            ["DueDate"] = invoice.DueDate?.ToString("d MMM yyyy"),
        };
        var subject = template is null
            ? $"{documentLabel} {documentNumber} from {business.Name}"
            : EmailTemplateRenderer.Render(template.SubjectTemplate, tokens);
        var intro = !string.IsNullOrWhiteSpace(customIntro)
            ? customIntro
            : template is null ? null : EmailTemplateRenderer.Render(template.BodyIntroTemplate, tokens);

        return new InvoiceEmailDraft(
            CustomerId: invoice.Job?.CustomerId,
            DefaultToAddress: customer?.EmailAddress,
            DefaultToName: customer is null ? null : $"{customer.FirstName} {customer.LastName}",
            Subject: subject,
            BodyHtml: BuildHtml(business, invoice, documentLabel, documentNumber, intro));
    }

    public async Task<ReminderEmailDraft?> ComposeReminderEmailAsync(
        Guid reminderId, string? customIntro, CancellationToken cancellationToken = default)
    {
        var reminder = await db.Reminders.AsNoTracking()
            .Include(r => r.Customer)
            .Include(r => r.Car).ThenInclude(c => c!.CarMake)
            .Include(r => r.Car).ThenInclude(c => c!.CarModel)
            .FirstOrDefaultAsync(r => r.Id == reminderId, cancellationToken);

        if (reminder is null)
        {
            return null;
        }

        var business = await businessDetailsService.GetCurrentAsync(cancellationToken);
        var customer = reminder.Customer;
        var customerName = customer is null ? null : $"{customer.FirstName} {customer.LastName}";
        var carLabel = reminder.Car is { } car ? $"{car.CarMake?.Name} {car.CarModel?.Name} ({car.Rego})" : null;
        var dueDate = reminder.DueDate.ToString("d MMM yyyy");

        var template = await emailTemplateService.GetByKeyAsync(EmailTemplateKeys.ReminderDue, cancellationToken);
        var tokens = new Dictionary<string, string?>
        {
            ["BusinessName"] = business.Name,
            ["CustomerName"] = customerName,
            ["ReminderTitle"] = reminder.Title,
            ["DueDate"] = dueDate,
            ["CarPlate"] = carLabel,
        };
        var subject = template is null
            ? $"Reminder: {reminder.Title}"
            : EmailTemplateRenderer.Render(template.SubjectTemplate, tokens);
        var intro = !string.IsNullOrWhiteSpace(customIntro)
            ? customIntro
            : template is null ? $"This is a reminder that \"{reminder.Title}\" is due {dueDate}." : EmailTemplateRenderer.Render(template.BodyIntroTemplate, tokens);

        return new ReminderEmailDraft(
            CustomerId: reminder.CustomerId,
            DefaultToAddress: customer?.EmailAddress,
            DefaultToName: customerName,
            Subject: subject,
            BodyHtml: BuildSimpleHtml(business, reminder.Title, intro, carLabel, $"Due {dueDate}"));
    }

    public async Task<AppointmentEmailDraft?> ComposeAppointmentEmailAsync(
        Guid appointmentId, string? customIntro, CancellationToken cancellationToken = default)
    {
        var appointment = await db.Appointments.AsNoTracking()
            .Include(a => a.Customer)
            .Include(a => a.Car).ThenInclude(c => c!.CarMake)
            .Include(a => a.Car).ThenInclude(c => c!.CarModel)
            .FirstOrDefaultAsync(a => a.Id == appointmentId, cancellationToken);

        if (appointment is null)
        {
            return null;
        }

        var business = await businessDetailsService.GetCurrentAsync(cancellationToken);
        var customer = appointment.Customer;
        // Soft-contact appointments (not yet converted) have no linked Customer — fall back to
        // the phone-call snapshot so a confirmation can still be sent.
        var toAddress = customer?.EmailAddress ?? appointment.ContactEmail;
        var toName = customer is null ? appointment.ContactName : $"{customer.FirstName} {customer.LastName}";
        var carLabel = appointment.Car is { } car ? $"{car.CarMake?.Name} {car.CarModel?.Name} ({car.Rego})" : appointment.VehicleDescription;
        var localStart = NzTime.FromUtc(appointment.StartUtc);
        var appointmentDate = localStart.ToString("d MMM yyyy");
        var appointmentTime = localStart.ToString("h:mm tt");

        var template = await emailTemplateService.GetByKeyAsync(EmailTemplateKeys.AppointmentConfirmation, cancellationToken);
        var tokens = new Dictionary<string, string?>
        {
            ["BusinessName"] = business.Name,
            ["CustomerName"] = toName,
            ["AppointmentTitle"] = appointment.Title,
            ["AppointmentDate"] = appointmentDate,
            ["AppointmentTime"] = appointmentTime,
            ["CarPlate"] = carLabel,
        };
        var subject = template is null
            ? $"Your appointment with {business.Name} — {appointmentDate}"
            : EmailTemplateRenderer.Render(template.SubjectTemplate, tokens);
        var intro = !string.IsNullOrWhiteSpace(customIntro)
            ? customIntro
            : template is null
                ? $"This confirms your appointment \"{appointment.Title}\" on {appointmentDate} at {appointmentTime}."
                : EmailTemplateRenderer.Render(template.BodyIntroTemplate, tokens);

        return new AppointmentEmailDraft(
            CustomerId: appointment.CustomerId,
            DefaultToAddress: toAddress,
            DefaultToName: toName,
            Subject: subject,
            BodyHtml: BuildSimpleHtml(business, appointment.Title, intro, carLabel, $"{appointmentDate} at {appointmentTime}"));
    }

    /// <summary>Shared letterhead+title+intro shell for the reminder/appointment emails — no
    /// line-items table (there's nothing to itemize), unlike <see cref="BuildHtml"/>.</summary>
    private static string BuildSimpleHtml(DTOs.BusinessDetailsDto business, string title, string? intro, string? carLabel, string? whenLabel)
    {
        string Enc(string? s) => WebUtility.HtmlEncode(s ?? "");

        var html = new StringBuilder();
        html.Append("<div style=\"font-family:Arial,Helvetica,sans-serif;color:#1e293b;max-width:640px\">");

        html.Append("<div style=\"margin-bottom:24px\">");
        html.Append($"<h2 style=\"margin:0 0 4px\">{Enc(business.Name)}</h2>");
        if (!string.IsNullOrWhiteSpace(business.Address)) html.Append($"<div style=\"color:#64748b;font-size:13px\">{Enc(business.Address)}</div>");
        if (!string.IsNullOrWhiteSpace(business.BusinessPhone)) html.Append($"<div style=\"color:#64748b;font-size:13px\">{Enc(business.BusinessPhone)}</div>");
        html.Append("</div>");

        html.Append($"<h3 style=\"margin:0 0 4px\">{Enc(title)}</h3>");
        if (!string.IsNullOrWhiteSpace(whenLabel))
        {
            html.Append($"<div style=\"color:#64748b;font-size:13px;margin-bottom:8px\">{Enc(whenLabel)}</div>");
        }

        if (!string.IsNullOrWhiteSpace(carLabel))
        {
            html.Append($"<div style=\"color:#64748b;font-size:13px;margin-bottom:16px\">Vehicle: {Enc(carLabel)}</div>");
        }

        if (!string.IsNullOrWhiteSpace(intro))
        {
            html.Append($"<p>{Enc(intro)}</p>");
        }

        html.Append("</div>");
        return html.ToString();
    }

    // Full line items/totals used to be duplicated here; now that the PDF attachment (see
    // InvoicePdfService) carries that detail, the body stays a short cover note instead.
    private static string BuildHtml(
        DTOs.BusinessDetailsDto business, Invoice invoice, string documentLabel, string documentNumber, string? customIntro)
    {
        string Enc(string? s) => WebUtility.HtmlEncode(s ?? "");
        string Money(decimal amount) => "$" + amount.ToString("N2", CultureInfo.InvariantCulture);

        var html = new StringBuilder();
        html.Append("<div style=\"font-family:Arial,Helvetica,sans-serif;color:#1e293b;max-width:640px\">");

        // Letterhead
        html.Append("<div style=\"margin-bottom:24px\">");
        html.Append($"<h2 style=\"margin:0 0 4px\">{Enc(business.Name)}</h2>");
        if (!string.IsNullOrWhiteSpace(business.Address)) html.Append($"<div style=\"color:#64748b;font-size:13px\">{Enc(business.Address)}</div>");
        if (!string.IsNullOrWhiteSpace(business.BusinessPhone)) html.Append($"<div style=\"color:#64748b;font-size:13px\">{Enc(business.BusinessPhone)}</div>");
        if (!string.IsNullOrWhiteSpace(business.GstNumber)) html.Append($"<div style=\"color:#64748b;font-size:13px\">GST No: {Enc(business.GstNumber)}</div>");
        html.Append("</div>");

        html.Append($"<h3 style=\"margin:0 0 4px\">{Enc(documentLabel)} {Enc(documentNumber)}</h3>");
        if (invoice.DueDate is { } dueDate)
        {
            html.Append($"<div style=\"color:#64748b;font-size:13px;margin-bottom:16px\">{(invoice.DocumentType == "Quotation" ? "Valid until" : "Due")} {dueDate:d MMM yyyy}</div>");
        }

        if (!string.IsNullOrWhiteSpace(customIntro))
        {
            html.Append($"<p>{Enc(customIntro)}</p>");
        }

        html.Append($"<p>Please find your {Enc(documentLabel.ToLowerInvariant())} attached as a PDF.</p>");

        html.Append("<table style=\"margin:16px 0;font-size:14px\">");
        html.Append(TotalsRow("Total", Money(invoice.TotalAmount), bold: true));
        html.Append("</table>");

        if (!string.IsNullOrWhiteSpace(business.BankDetails))
        {
            html.Append("<div style=\"margin-top:24px;padding-top:12px;border-top:1px solid #e2e8f0;font-size:13px;color:#475569\">");
            html.Append($"<strong>Payment details</strong><br/>{Enc(business.BankDetails).Replace("\n", "<br/>")}");
            html.Append("</div>");
        }

        html.Append("</div>");
        return html.ToString();
    }

    private static string TotalsRow(string label, string value, bool bold = false)
    {
        var weight = bold ? "font-weight:bold" : "";
        return $"<tr><td style=\"padding:2px 4px;{weight}\">{label}</td><td style=\"padding:2px 4px;text-align:right;{weight}\">{value}</td></tr>";
    }
}
