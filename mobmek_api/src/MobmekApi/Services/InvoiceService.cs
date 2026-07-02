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

        // GST is added on top of the subtotal. The rate is snapshotted from the GstSetting entity.
        var gstRate = (await gstSettingService.GetCurrentAsync(cancellationToken)).Rate;
        var taxAmount = Round(subTotal * gstRate);
        var totalAmount = Round(subTotal + taxAmount);

        // Business-wide sequential number for the printed invoice ID (INV-0001, ...).
        var nextSequenceNumber = (await db.Invoices.MaxAsync(i => (int?)i.SequenceNumber, cancellationToken) ?? 0) + 1;

        var invoice = new Invoice
        {
            JobId = jobId,
            SequenceNumber = nextSequenceNumber,
            IssueName = job.Title,
            Notes = job.InvoiceNotes,
            DocumentType = "Invoice",
            Status = "Active",
            DueDate = request.DueDate,
            LabourPrice = labourTotal,
            SubTotal = subTotal,
            GstRate = gstRate,
            TaxAmount = taxAmount,
            Discount = 0m,
            ShippingFee = 0m,
            TotalAmount = totalAmount,
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

        // A rejected invoice no longer represents money received; drop any ledger rows
        // its payment posted.
        await RemoveLedgerPostingsAsync(invoice.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(invoice);
    }

    public async Task<InvoiceDto?> MarkPaidAsync(Guid jobId, Guid id, MarkInvoicePaidRequest request, CancellationToken cancellationToken = default)
    {
        var invoice = await db.Invoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id && i.JobId == jobId, cancellationToken);

        // Only an active invoice can be paid — a rejected one stays rejected.
        if (invoice is null || invoice.Status == "Rejected")
        {
            return null;
        }

        invoice.IsPaid = true;
        invoice.DatePaid = request.DatePaid ?? DateOnly.FromDateTime(DateTime.UtcNow);
        invoice.AmountPaid = invoice.TotalAmount;
        invoice.CashAmount = request.CashAmount;
        invoice.CardAmount = request.CardAmount;
        invoice.ModeOfPayment = string.IsNullOrWhiteSpace(request.ModeOfPayment) ? null : request.ModeOfPayment;
        invoice.PaymentTerm = string.IsNullOrWhiteSpace(request.PaymentTerm) ? null : request.PaymentTerm;

        await PostPaymentToLedgerAsync(invoice, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(invoice);
    }

    /// <summary>
    /// Posts the payment into the cash-flow ledger, routed by the CashFlowSettings account
    /// mapping: the cash portion to the cash account, the card portion to the card account,
    /// and any remainder by the mode-of-payment text — each falling back to the default
    /// account. Portions with no resolvable account (module not set up) are skipped, so
    /// invoicing works before any cash accounts exist. Re-marking an invoice paid replaces
    /// its earlier postings instead of doubling them.
    /// </summary>
    private async Task PostPaymentToLedgerAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        await RemoveLedgerPostingsAsync(invoice.Id, cancellationToken);

        var settings = await db.CashFlowSettings.OrderBy(s => s.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            return;
        }

        // Guard against routes pointing at accounts that no longer exist or are archived.
        var routable = new[] { settings.DefaultAccountId, settings.CashAccountId, settings.CardAccountId, settings.BankTransferAccountId }
            .Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        var validIds = await db.CashAccounts
            .Where(a => routable.Contains(a.Id) && !a.IsArchived)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        Guid? Route(Guid? specific)
        {
            var id = specific ?? settings.DefaultAccountId;
            return id is not null && validIds.Contains(id.Value) ? id : null;
        }

        var legs = new List<(Guid AccountId, decimal Amount)>();
        var cashPortion = invoice.CashAmount ?? 0m;
        var cardPortion = invoice.CardAmount ?? 0m;

        if (cashPortion > 0 && Route(settings.CashAccountId) is { } cashAccountId)
        {
            legs.Add((cashAccountId, cashPortion));
        }

        if (cardPortion > 0 && Route(settings.CardAccountId) is { } cardAccountId)
        {
            legs.Add((cardAccountId, cardPortion));
        }

        // Whatever the explicit cash/card split doesn't cover routes by the free-text
        // payment mode (a plain "Bank Transfer" payment lands here in full).
        var remainder = (invoice.AmountPaid ?? invoice.TotalAmount) - cashPortion - cardPortion;
        if (remainder > 0)
        {
            var mode = invoice.ModeOfPayment?.ToLowerInvariant() ?? string.Empty;
            var specific = mode.Contains("cash") ? settings.CashAccountId
                : mode.Contains("card") || mode.Contains("eftpos") ? settings.CardAccountId
                : mode.Contains("transfer") || mode.Contains("bank") ? settings.BankTransferAccountId
                : null;
            if (Route(specific) is { } remainderAccountId)
            {
                legs.Add((remainderAccountId, remainder));
            }
        }

        if (legs.Count == 0)
        {
            return;
        }

        var category = await CashFlowSeeder.EnsureSystemCategoryAsync(db, CashFlowSeeder.WorkshopSalesCategory, cancellationToken);
        var customerName = await db.Jobs
            .Where(j => j.Id == invoice.JobId)
            .Select(j => j.Customer!.FirstName + " " + j.Customer.LastName)
            .FirstOrDefaultAsync(cancellationToken);

        foreach (var (accountId, amount) in legs)
        {
            db.CashTransactions.Add(new CashTransaction
            {
                AccountId = accountId,
                Direction = "In",
                Amount = amount,
                Date = invoice.DatePaid ?? DateOnly.FromDateTime(DateTime.UtcNow),
                Description = $"Invoice INV-{invoice.SequenceNumber:D4} — {invoice.IssueName}",
                CategoryId = category.Id,
                Counterparty = customerName,
                InvoiceId = invoice.Id,
                GstTreatment = "Taxable",
            });
        }
    }

    private async Task RemoveLedgerPostingsAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        var postings = await db.CashTransactions
            .Where(t => t.InvoiceId == invoiceId)
            .ToListAsync(cancellationToken);
        db.CashTransactions.RemoveRange(postings);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static InvoiceDto ToDto(Invoice i) =>
        new(i.Id, i.JobId, $"INV-{i.SequenceNumber:D4}", i.IssueName, i.Notes, i.DocumentType, i.Status, i.DueDate, i.PaymentTerm, i.ModeOfPayment,
            i.LabourPrice, i.SubTotal, i.GstRate, i.TaxAmount, i.Discount, i.ShippingFee, i.TotalAmount,
            i.IsPaid, i.AmountPaid, i.DatePaid, i.CashAmount, i.CardAmount,
            i.Items.OrderBy(x => x.CreatedAtUtc)
                .Select(x => new InvoiceItemDto(x.Id, x.InvoiceId, x.ItemName, x.Quantity, x.ItemPrice, x.ItemTotal))
                .ToList(),
            i.CreatedAtUtc, i.UpdatedAtUtc);
}
