using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class InvoiceService(AppDbContext db, IGstSettingService gstSettingService) : IInvoiceService
{
    private const int MaxPageSize = 200;

    public async Task<IReadOnlyList<InvoiceDto>> GetAllAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var invoices = await db.Invoices.AsNoTracking()
            .Where(i => i.JobId == jobId)
            .Include(i => i.Items)
            .Include(i => i.Job).ThenInclude(j => j!.Customer)
            .Include(i => i.Job).ThenInclude(j => j!.Car).ThenInclude(c => c!.CarMake)
            .Include(i => i.Job).ThenInclude(j => j!.Car).ThenInclude(c => c!.CarModel)
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var latestEmailByInvoiceId = await GetLatestEmailsAsync(invoices.Select(i => i.Id), cancellationToken);
        return invoices.Select(i => ToDto(i, latestEmailByInvoiceId.GetValueOrDefault(i.Id))).ToList();
    }

    public async Task<PagedResult<InvoiceListItemDto>> GetPagedAsync(
        string documentType, int page, int pageSize, string? search,
        string? sortBy = null, string? status = null, bool? isPaid = null,
        DateOnly? dateFrom = null, DateOnly? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.Invoices.AsNoTracking().Where(i => i.DocumentType == documentType);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowerTerm = search.Trim().ToLower();
            query = query.Where(i =>
                (i.Job != null && i.Job.Customer != null &&
                    (i.Job.Customer.FirstName + " " + i.Job.Customer.LastName).ToLower().Contains(lowerTerm)) ||
                (i.Job != null && i.Job.Car != null && i.Job.Car.Rego.ToLower().Contains(lowerTerm)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(i => i.Status == status);
        }

        if (isPaid is { } isPaidValue)
        {
            query = query.Where(i => i.IsPaid == isPaidValue);
        }

        query = ApplyDateRange(query, dateFrom, dateTo);

        var totalCount = await query.CountAsync(cancellationToken);

        query = sortBy switch
        {
            "oldest" => query.OrderBy(i => i.CreatedAtUtc),
            "amountDesc" => query.OrderByDescending(i => i.TotalAmount),
            "amountAsc" => query.OrderBy(i => i.TotalAmount),
            _ => query.OrderByDescending(i => i.CreatedAtUtc),
        };

        var items = await query
            .Include(i => i.Job).ThenInclude(j => j!.Customer)
            .Include(i => i.Job).ThenInclude(j => j!.Car).ThenInclude(c => c!.CarMake)
            .Include(i => i.Job).ThenInclude(j => j!.Car).ThenInclude(c => c!.CarModel)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<InvoiceListItemDto>(items.Select(ToListItemDto).ToList(), totalCount, page, pageSize);
    }

    // Npgsql only accepts UTC-kinded DateTimes against "timestamp with time zone" columns, so
    // the inclusive DateOnly bounds are converted to a [from 00:00, to+1day 00:00) UTC range.
    private static IQueryable<Invoice> ApplyDateRange(IQueryable<Invoice> query, DateOnly? dateFrom, DateOnly? dateTo)
    {
        if (dateFrom is { } from)
        {
            var fromUtc = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            query = query.Where(i => i.CreatedAtUtc >= fromUtc);
        }

        if (dateTo is { } to)
        {
            var toUtcExclusive = DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            query = query.Where(i => i.CreatedAtUtc < toUtcExclusive);
        }

        return query;
    }

    public async Task<InvoiceDto?> GetByIdAsync(Guid jobId, Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await db.Invoices.AsNoTracking()
            .Include(i => i.Items)
            .Include(i => i.Job).ThenInclude(j => j!.Customer)
            .Include(i => i.Job).ThenInclude(j => j!.Car).ThenInclude(c => c!.CarMake)
            .Include(i => i.Job).ThenInclude(j => j!.Car).ThenInclude(c => c!.CarModel)
            .FirstOrDefaultAsync(i => i.Id == id && i.JobId == jobId, cancellationToken);

        if (invoice is null)
        {
            return null;
        }

        var latestEmail = await db.OutboundEmails.AsNoTracking()
            .Where(e => e.InvoiceId == id)
            .OrderByDescending(e => e.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return ToDto(invoice, latestEmail);
    }

    /// <summary>Most recent <see cref="OutboundEmail"/> per invoice id, for the list view's email column.</summary>
    private async Task<Dictionary<Guid, OutboundEmail>> GetLatestEmailsAsync(IEnumerable<Guid> invoiceIds, CancellationToken cancellationToken)
    {
        var ids = invoiceIds.ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var latest = await db.OutboundEmails.AsNoTracking()
            .Where(e => e.InvoiceId != null && ids.Contains(e.InvoiceId!.Value))
            .GroupBy(e => e.InvoiceId!.Value)
            .Select(g => g.OrderByDescending(e => e.CreatedAtUtc).First())
            .ToListAsync(cancellationToken);

        return latest.ToDictionary(e => e.InvoiceId!.Value);
    }

    public Task<InvoiceDto?> GenerateAsync(Guid jobId, CreateInvoiceRequest request, CancellationToken cancellationToken = default) =>
        GenerateDocumentAsync(jobId, request, "Invoice", cancellationToken);

    public Task<InvoiceDto?> GenerateQuotationAsync(Guid jobId, CreateInvoiceRequest request, CancellationToken cancellationToken = default) =>
        GenerateDocumentAsync(jobId, request, "Quotation", cancellationToken);

    private async Task<InvoiceDto?> GenerateDocumentAsync(Guid jobId, CreateInvoiceRequest request, string documentType, CancellationToken cancellationToken)
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

        // The job's discount is snapshotted as a dollar amount, same as every other money
        // field on the invoice — later edits to the job's discount don't change it.
        var discount = DiscountCalculator.ComputeAmount(job.DiscountType, job.DiscountValue, subTotal);

        // GST is added on top of the discounted subtotal. The rate is snapshotted from the GstSetting entity.
        var gstRate = (await gstSettingService.GetCurrentAsync(cancellationToken)).Rate;
        var taxAmount = Round((subTotal - discount) * gstRate);
        var totalAmount = Round(subTotal - discount + taxAmount);

        // Business-wide sequential number for the printed document ID, counted per document
        // type so invoices (INV-0001, ...) and quotations (QUO-0001, ...) number independently.
        var nextSequenceNumber = (await db.Invoices
            .Where(i => i.DocumentType == documentType)
            .MaxAsync(i => (int?)i.SequenceNumber, cancellationToken) ?? 0) + 1;

        var invoice = new Invoice
        {
            JobId = jobId,
            SequenceNumber = nextSequenceNumber,
            IssueName = job.Title,
            Notes = job.InvoiceNotes,
            DocumentType = documentType,
            Status = "Active",
            // Policy: a quotation is valid for exactly 30 days after issue; an invoice's
            // due date is caller-supplied.
            DueDate = documentType == "Quotation"
                ? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30)
                : request.DueDate,
            LabourPrice = labourTotal,
            SubTotal = subTotal,
            GstRate = gstRate,
            TaxAmount = taxAmount,
            Discount = discount,
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

    public async Task<InvoiceDto?> AcceptQuotationAsync(Guid jobId, Guid id, AcceptQuotationRequest request, CancellationToken cancellationToken = default)
    {
        var quotation = await db.Invoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id && i.JobId == jobId, cancellationToken);

        // Only an active quotation can be accepted — never a plain invoice, and an
        // already-accepted or rejected quotation stays as it is.
        if (quotation is null || quotation.DocumentType != "Quotation" || quotation.Status != "Active")
        {
            return null;
        }

        var nextSequenceNumber = (await db.Invoices
            .Where(i => i.DocumentType == "Invoice")
            .MaxAsync(i => (int?)i.SequenceNumber, cancellationToken) ?? 0) + 1;

        // The invoice copies the quotation's snapshot, not the job's current lines: the
        // customer pays exactly what they accepted.
        var invoice = new Invoice
        {
            JobId = quotation.JobId,
            SequenceNumber = nextSequenceNumber,
            IssueName = quotation.IssueName,
            Notes = quotation.Notes,
            DocumentType = "Invoice",
            Status = "Active",
            DueDate = request.DueDate,
            LabourPrice = quotation.LabourPrice,
            SubTotal = quotation.SubTotal,
            GstRate = quotation.GstRate,
            TaxAmount = quotation.TaxAmount,
            Discount = quotation.Discount,
            ShippingFee = quotation.ShippingFee,
            TotalAmount = quotation.TotalAmount,
        };

        foreach (var item in quotation.Items.OrderBy(i => i.CreatedAtUtc))
        {
            invoice.Items.Add(new InvoiceItem
            {
                ItemName = item.ItemName,
                Quantity = item.Quantity,
                ItemPrice = item.ItemPrice,
                ItemTotal = item.ItemTotal,
            });
        }

        quotation.Status = "Accepted";
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

        // Only an active invoice can be paid — a rejected one stays rejected, and a
        // quotation is never payable (it must be issued as an invoice first).
        if (invoice is null || invoice.Status == "Rejected" || invoice.DocumentType == "Quotation")
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

    private static InvoiceListItemDto ToListItemDto(Invoice i) =>
        new(i.Id, i.JobId, $"{(i.DocumentType == "Quotation" ? "QUO" : "INV")}-{i.SequenceNumber:D4}", i.IssueName, i.DocumentType, i.Status,
            i.Job?.Customer is { } customer ? $"{customer.FirstName} {customer.LastName}" : null,
            i.Job?.Car is { } car ? $"{car.CarMake?.Name} {car.CarModel?.Name} ({car.Rego})" : null,
            i.DueDate, i.TotalAmount, i.IsPaid, i.CreatedAtUtc);

    private static InvoiceDto ToDto(Invoice i, OutboundEmail? latestEmail = null) =>
        new(i.Id, i.JobId, $"{(i.DocumentType == "Quotation" ? "QUO" : "INV")}-{i.SequenceNumber:D4}", i.IssueName, i.Notes, i.DocumentType, i.Status, i.DueDate, i.PaymentTerm, i.ModeOfPayment,
            i.LabourPrice, i.SubTotal, i.GstRate, i.TaxAmount, i.Discount, i.ShippingFee, i.TotalAmount,
            i.IsPaid, i.AmountPaid, i.DatePaid, i.CashAmount, i.CardAmount,
            i.Items.OrderBy(x => x.CreatedAtUtc)
                .Select(x => new InvoiceItemDto(x.Id, x.InvoiceId, x.ItemName, x.Quantity, x.ItemPrice, x.ItemTotal))
                .ToList(),
            i.CreatedAtUtc, i.UpdatedAtUtc,
            i.Job?.Customer is { } customer ? $"{customer.FirstName} {customer.LastName}" : null,
            i.Job?.Customer?.EmailAddress,
            i.Job?.Car is { } car ? $"{car.CarMake?.Name} {car.CarModel?.Name} ({car.Rego})" : null,
            latestEmail?.Status, latestEmail?.CreatedAtUtc);
}
