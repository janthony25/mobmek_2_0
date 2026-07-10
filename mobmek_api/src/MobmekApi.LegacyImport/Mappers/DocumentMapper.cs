using MobmekApi.Entities;
using MobmekApi.LegacyImport.Legacy;

namespace MobmekApi.LegacyImport.Mappers;

/// <summary>
/// Maps the four legacy document tables to the new single Invoice entity (design §3.5).
/// All money fields are snapshots copied verbatim with null → 0 — never recomputed. The
/// GST rate is the constant 0.15: legacy tax was always 15% but added on top of
/// (SubTotal + labour + shipping − discount), so deriving it from TaxAmount/SubTotal would
/// be wrong (real ratios scatter 0.146–0.455). SequenceNumber stays 0 here; the
/// document-sequences phase assigns printed numbers chronologically afterwards.
/// </summary>
public static class DocumentMapper
{
    public const decimal LegacyGstRate = 0.15m;

    public const int ItemNameMaxLength = 255;

    private const int IssueNameMaxLength = 255;

    private const int NotesMaxLength = 4000;

    public static Invoice Map(LegacyInvoice legacy, Guid jobId)
    {
        var invoice = MapCore(jobId, "Invoice", legacy.IssueName, legacy.Notes, "Active",
            legacy.LaborPrice, legacy.Discount, legacy.ShippingFee, legacy.SubTotal,
            legacy.TaxAmount, legacy.TotalAmount, legacy.IsPaid, legacy.AmountPaid,
            legacy.DueDate, legacy.DateAdded, legacy.DateEdited);
        invoice.PaymentTerm = TrimToNull(legacy.PaymentTerm);
        return invoice;
    }

    public static Invoice Map(LegacyQuotation legacy, Guid jobId) =>
        MapCore(jobId, "Quotation", legacy.IssueName, legacy.Notes, "Active",
            legacy.LaborPrice, legacy.Discount, legacy.ShippingFee, legacy.SubTotal,
            legacy.TaxAmount, legacy.TotalAmount, isPaid: false, amountPaid: null,
            dueDateNz: null, legacy.DateAdded, legacy.DateEdited);

    public static Invoice Map(LegacyNewInvoice legacy, Guid jobId)
    {
        var invoice = MapCore(jobId, "Invoice", legacy.IssueName, legacy.Notes, legacy.Status.Trim(),
            legacy.LabourPrice, legacy.Discount, legacy.ShippingFee, legacy.SubTotal,
            legacy.TaxAmount, legacy.TotalAmount, legacy.IsPaid, legacy.AmountPaid,
            legacy.DueDate, legacy.DateAdded, legacy.DateEdited);
        invoice.PaymentTerm = TrimToNull(legacy.PaymentTerm);
        invoice.ModeOfPayment = TrimToNull(legacy.ModeOfPayment);
        invoice.DatePaid = NzTime.ToDateOnly(legacy.DatePaid);
        invoice.CashAmount = legacy.CashAmount;
        invoice.CardAmount = legacy.CardAmount;
        return invoice;
    }

    public static Invoice Map(LegacyNewQuotation legacy, Guid jobId)
    {
        // The new model has no accepted flag (§3.5) — preserved as a notes suffix.
        var notes = TrimToNull(legacy.Notes);
        if (legacy.IsAccepted)
        {
            notes = notes is null ? "[Accepted in legacy system]" : $"{notes}\n[Accepted in legacy system]";
        }

        var invoice = MapCore(jobId, "Quotation", legacy.IssueName, notes, legacy.Status.Trim(),
            legacy.LabourPrice, legacy.Discount, legacy.ShippingFee, legacy.SubTotal,
            legacy.TaxAmount, legacy.TotalAmount, isPaid: false, amountPaid: null,
            legacy.ValidUntil, legacy.DateAdded, legacy.DateEdited);
        invoice.PaymentTerm = TrimToNull(legacy.PaymentTerm);
        invoice.ModeOfPayment = TrimToNull(legacy.ModeOfPayment);
        return invoice;
    }

    /// <summary>Item line shared by all four item tables; a stored ItemTotal wins over Quantity × ItemPrice.</summary>
    public static InvoiceItem MapItem(Guid invoiceId, string itemName, int quantity, decimal itemPrice, decimal? itemTotal, DateTime createdAtUtc) => new()
    {
        InvoiceId = invoiceId,
        ItemName = Truncate(itemName.Trim(), ItemNameMaxLength),
        Quantity = quantity,
        ItemPrice = itemPrice,
        ItemTotal = itemTotal ?? quantity * itemPrice,
        CreatedAtUtc = createdAtUtc,
    };

    private static Invoice MapCore(
        Guid jobId,
        string documentType,
        string issueName,
        string? notes,
        string status,
        decimal? labourPrice,
        decimal? discount,
        decimal? shippingFee,
        decimal? subTotal,
        decimal? taxAmount,
        decimal? totalAmount,
        bool isPaid,
        decimal? amountPaid,
        DateTime? dueDateNz,
        DateTime? dateAddedNz,
        DateTime? dateEditedNz) => new()
    {
        JobId = jobId,
        DocumentType = documentType,
        IssueName = Truncate(issueName.Trim(), IssueNameMaxLength),
        Notes = TrimToNull(notes) is { } n ? Truncate(n, NotesMaxLength) : null,
        Status = status,
        SequenceNumber = 0,
        LabourPrice = labourPrice ?? 0,
        Discount = discount ?? 0,
        ShippingFee = shippingFee ?? 0,
        SubTotal = subTotal ?? 0,
        GstRate = LegacyGstRate,
        TaxAmount = taxAmount ?? 0,
        TotalAmount = totalAmount ?? 0,
        IsPaid = isPaid,
        AmountPaid = amountPaid,
        DueDate = NzTime.ToDateOnly(dueDateNz),
        CreatedAtUtc = dateAddedNz is null ? DateTime.UtcNow : NzTime.ToUtc(dateAddedNz.Value),
        UpdatedAtUtc = NzTime.ToUtc(dateEditedNz),
    };

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
