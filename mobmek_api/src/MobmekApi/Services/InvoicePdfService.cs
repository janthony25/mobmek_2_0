using System.Globalization;
using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MobmekApi.Services;

public class InvoicePdfService(AppDbContext db, IBusinessDetailsService businessDetailsService) : IInvoicePdfService
{
    public async Task<InvoicePdfDocument?> GenerateAsync(Guid jobId, Guid invoiceId, CancellationToken cancellationToken = default)
    {
        // Same query shape as EmailComposeService.ComposeInvoiceEmailAsync — this is a pure
        // rendering of the same data, just as a PDF instead of an HTML email body.
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
        var logoBytes = await ReadLogoAsync(cancellationToken);

        var isQuotation = invoice.DocumentType == "Quotation";
        var documentLabel = isQuotation ? "Quotation" : "Invoice";
        var documentNumber = $"{(isQuotation ? "QUO" : "INV")}-{invoice.SequenceNumber:D4}";
        var customer = invoice.Job?.Customer;
        var car = invoice.Job?.Car;
        var carDescription = car is null ? null : $"{car.CarMake?.Name} {car.CarModel?.Name} ({car.Rego})";

        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));

                page.Header().Element(c => ComposeHeader(c, business, logoBytes, documentLabel, documentNumber));
                page.Content().Element(c => ComposeContent(c, invoice, business, isQuotation, customer, carDescription));
                page.Footer().AlignCenter().Text("Thank you for your business.").FontSize(9).FontColor(Colors.Grey.Medium);
            });
        }).GeneratePdf();

        return new InvoicePdfDocument(bytes, $"{documentNumber}.pdf");
    }

    private async Task<byte[]?> ReadLogoAsync(CancellationToken cancellationToken)
    {
        var logo = await businessDetailsService.GetLogoAsync(cancellationToken);
        if (logo is null)
        {
            return null;
        }

        var (_, _, content) = logo.Value;
        await using var _ = content;
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    private static void ComposeHeader(
        IContainer container, BusinessDetailsDto business, byte[]? logoBytes, string documentLabel, string documentNumber)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                if (logoBytes is not null)
                {
                    column.Item().Width(100).Image(logoBytes).FitWidth();
                }

                column.Item().Text(business.Name).FontSize(16).Bold();
                if (!string.IsNullOrWhiteSpace(business.Address))
                {
                    column.Item().Text(business.Address).FontSize(9).FontColor(Colors.Grey.Medium);
                }

                var contact = string.Join(" · ", new[] { business.BusinessPhone, business.Telephone, business.Email, business.Website }
                    .Where(v => !string.IsNullOrWhiteSpace(v)));
                if (contact.Length > 0)
                {
                    column.Item().Text(contact).FontSize(9).FontColor(Colors.Grey.Medium);
                }
            });

            row.ConstantItem(180).Column(column =>
            {
                column.Item().AlignRight().Text(documentLabel.ToUpperInvariant()).FontSize(18).Bold();
                column.Item().AlignRight().Text($"{documentLabel} ID: {documentNumber}").FontSize(9).FontColor(Colors.Grey.Medium);
                if (!string.IsNullOrWhiteSpace(business.GstNumber))
                {
                    column.Item().AlignRight().Text($"GST No: {business.GstNumber}").FontSize(9).FontColor(Colors.Grey.Medium);
                }
            });
        });
    }

    private static void ComposeContent(
        IContainer container, Invoice invoice, BusinessDetailsDto business, bool isQuotation, Customer? customer, string? carDescription)
    {
        string Money(decimal amount) => "$" + amount.ToString("N2", CultureInfo.InvariantCulture);

        container.PaddingTop(20).Column(column =>
        {
            column.Spacing(16);

            column.Item().Row(row =>
            {
                row.RelativeItem().Column(billTo =>
                {
                    billTo.Item().Text("Bill to").FontSize(8).FontColor(Colors.Grey.Medium);
                    billTo.Item().Text(customer is null ? "—" : $"{customer.FirstName} {customer.LastName}").Bold();
                    billTo.Item().Text(carDescription ?? "—").FontColor(Colors.Grey.Darken1);
                });

                row.ConstantItem(180).Column(dates =>
                {
                    dates.Item().AlignRight().Text($"Issued: {invoice.CreatedAtUtc:d MMM yyyy}").FontSize(9);
                    dates.Item().AlignRight().Text(
                        $"{(isQuotation ? "Valid until" : "Due")}: {(invoice.DueDate is { } due ? due.ToString("d MMM yyyy") : "—")}")
                        .FontSize(9);
                });
            });

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(4);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    header.Cell().Text("Description").FontSize(9).FontColor(Colors.Grey.Medium);
                    header.Cell().AlignRight().Text("Qty").FontSize(9).FontColor(Colors.Grey.Medium);
                    header.Cell().AlignRight().Text("Unit price").FontSize(9).FontColor(Colors.Grey.Medium);
                    header.Cell().AlignRight().Text("Total").FontSize(9).FontColor(Colors.Grey.Medium);
                    header.Cell().ColumnSpan(4).PaddingTop(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten1);
                });

                foreach (var item in invoice.Items.OrderBy(i => i.CreatedAtUtc))
                {
                    table.Cell().PaddingVertical(4).Text(item.ItemName);
                    table.Cell().PaddingVertical(4).AlignRight().Text(item.Quantity.ToString());
                    table.Cell().PaddingVertical(4).AlignRight().Text(Money(item.ItemPrice));
                    table.Cell().PaddingVertical(4).AlignRight().Text(Money(item.ItemTotal));
                    table.Cell().ColumnSpan(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                }
            });

            column.Item().AlignRight().Width(220).Column(totals =>
            {
                totals.Item().Row(r => { r.RelativeItem().Text("Subtotal").FontColor(Colors.Grey.Medium); r.ConstantItem(90).AlignRight().Text(Money(invoice.SubTotal)); });
                if (invoice.Discount > 0)
                {
                    totals.Item().Row(r => { r.RelativeItem().Text("Discount").FontColor(Colors.Grey.Medium); r.ConstantItem(90).AlignRight().Text($"-{Money(invoice.Discount)}"); });
                }
                totals.Item().Row(r => { r.RelativeItem().Text($"GST ({invoice.GstRate:P0} incl.)").FontColor(Colors.Grey.Medium); r.ConstantItem(90).AlignRight().Text(Money(invoice.TaxAmount)); });
                if (invoice.ShippingFee > 0)
                {
                    totals.Item().Row(r => { r.RelativeItem().Text("Shipping").FontColor(Colors.Grey.Medium); r.ConstantItem(90).AlignRight().Text(Money(invoice.ShippingFee)); });
                }
                totals.Item().PaddingTop(4).BorderTop(1).BorderColor(Colors.Grey.Lighten1).Row(r =>
                {
                    r.RelativeItem().Text("Total").Bold();
                    r.ConstantItem(90).AlignRight().Text(Money(invoice.TotalAmount)).Bold();
                });
            });

            if (invoice.IsPaid)
            {
                column.Item().Background(Colors.Green.Lighten5).Padding(10).Column(paid =>
                {
                    paid.Item().Text($"Paid {invoice.DatePaid:d MMM yyyy}").Bold().FontColor(Colors.Green.Darken2);
                    var details = string.Join(" · ", new[]
                    {
                        invoice.ModeOfPayment is { } mode ? $"Mode: {mode}" : null,
                        invoice.AmountPaid is { } paidAmt ? $"Amount: {Money(paidAmt)}" : null,
                    }.Where(v => v is not null));
                    if (details.Length > 0)
                    {
                        paid.Item().Text(details).FontSize(9).FontColor(Colors.Green.Darken1);
                    }
                });
            }

            if (!string.IsNullOrWhiteSpace(invoice.Notes))
            {
                column.Item().Column(notes =>
                {
                    notes.Item().Text("Notes").FontSize(8).FontColor(Colors.Grey.Medium);
                    notes.Item().Text(invoice.Notes);
                });
            }

            if (isQuotation)
            {
                column.Item().Background(Colors.Grey.Lighten4).Padding(10)
                    .Text("This quotation is valid only for 30 days after the issue date.").FontSize(9);
            }

            if (!string.IsNullOrWhiteSpace(business.BankDetails))
            {
                column.Item().Column(bank =>
                {
                    bank.Item().Text("Payment details").FontSize(8).FontColor(Colors.Grey.Medium);
                    bank.Item().Text(business.BankDetails);
                });
            }
        });
    }
}
