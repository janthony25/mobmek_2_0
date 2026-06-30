using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class InvoiceService(AppDbContext db, IGstSettingService gstSettingService) : IInvoiceService
{
    public async Task<IReadOnlyList<InvoiceDto>> GetAllAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var invoices = await db.Invoices.AsNoTracking()
            .Where(i => i.JobId == jobId)
            .Include(i => i.Items)
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return invoices.Select(ToDto).ToList();
    }

    public async Task<InvoiceDto?> GetByIdAsync(Guid jobId, Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await db.Invoices.AsNoTracking()
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id && i.JobId == jobId, cancellationToken);

        return invoice is null ? null : ToDto(invoice);
    }

    public async Task<InvoiceDto?> GenerateAsync(Guid jobId, CreateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var job = await db.Jobs
            .Include(j => j.Items)
            .Include(j => j.Labour)
            .Include(j => j.ServiceLines).ThenInclude(s => s.JobService)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        if (job is null)
        {
            return null;
        }

        var labourTotal = job.Labour.Sum(l => l.TotalAmount);
        var itemsTotal = job.Items.Sum(i => i.ItemTotal);
        var servicesTotal = job.ServiceLines.Sum(s => s.LineTotal);
        var subTotal = Round(itemsTotal + labourTotal + servicesTotal);

        // GST is inclusive: already part of the prices, so it's recorded for display, not added on top.
        var gstRate = (await gstSettingService.GetCurrentAsync(cancellationToken)).Rate;
        var taxAmount = Round(subTotal * gstRate);

        var invoice = new Invoice
        {
            JobId = jobId,
            IssueName = job.Title,
            Notes = job.InvoiceNotes,
            DocumentType = "Invoice",
            Status = "Active",
            DueDate = request.DueDate,
            ModeOfPayment = string.IsNullOrWhiteSpace(request.ModeOfPayment) ? null : request.ModeOfPayment,
            PaymentTerm = string.IsNullOrWhiteSpace(request.PaymentTerm) ? null : request.PaymentTerm,
            LabourPrice = labourTotal,
            SubTotal = subTotal,
            GstRate = gstRate,
            TaxAmount = taxAmount,
            Discount = 0m,
            ShippingFee = 0m,
            TotalAmount = subTotal,
        };

        // Snapshot each part of the job as its own invoice line.
        foreach (var item in job.Items.OrderBy(i => i.CreatedAtUtc))
        {
            invoice.Items.Add(new InvoiceItem
            {
                ItemName = item.ItemName,
                Quantity = item.ItemQuantity,
                ItemPrice = item.SellingPrice,
                ItemTotal = item.ItemTotal,
            });
        }

        if (labourTotal > 0)
        {
            invoice.Items.Add(new InvoiceItem
            {
                ItemName = "Labour",
                Quantity = 1,
                ItemPrice = labourTotal,
                ItemTotal = labourTotal,
            });
        }

        foreach (var line in job.ServiceLines.OrderBy(s => s.CreatedAtUtc))
        {
            invoice.Items.Add(new InvoiceItem
            {
                ItemName = line.JobService?.Name ?? "Service",
                Quantity = line.Quantity,
                ItemPrice = line.UnitPrice,
                ItemTotal = line.LineTotal,
            });
        }

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(invoice);
    }

    public async Task<InvoiceDto?> RejectAsync(Guid jobId, Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await db.Invoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id && i.JobId == jobId, cancellationToken);

        if (invoice is null)
        {
            return null;
        }

        invoice.Status = "Rejected";
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(invoice);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static InvoiceDto ToDto(Invoice i) =>
        new(i.Id, i.JobId, i.IssueName, i.Notes, i.DocumentType, i.Status, i.DueDate, i.PaymentTerm, i.ModeOfPayment,
            i.LabourPrice, i.SubTotal, i.GstRate, i.TaxAmount, i.Discount, i.ShippingFee, i.TotalAmount,
            i.Items.OrderBy(x => x.CreatedAtUtc)
                .Select(x => new InvoiceItemDto(x.Id, x.InvoiceId, x.ItemName, x.Quantity, x.ItemPrice, x.ItemTotal))
                .ToList(),
            i.CreatedAtUtc, i.UpdatedAtUtc);
}
