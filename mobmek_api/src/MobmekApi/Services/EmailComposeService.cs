using System.Globalization;
using System.Net;
using System.Text;
using MobmekApi.Data;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class EmailComposeService(AppDbContext db, IBusinessDetailsService businessDetailsService) : IEmailComposeService
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
        var documentNumber = $"{(invoice.DocumentType == "Quotation" ? "QUO" : "INV")}-{invoice.SequenceNumber:D4}";

        return new InvoiceEmailDraft(
            CustomerId: invoice.Job?.CustomerId,
            DefaultToAddress: customer?.EmailAddress,
            DefaultToName: customer is null ? null : $"{customer.FirstName} {customer.LastName}",
            Subject: $"{documentLabel} {documentNumber} from {business.Name}",
            BodyHtml: BuildHtml(business, invoice, documentLabel, documentNumber, customIntro));
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
