using MobmekApi.LegacyImport.Legacy;
using MobmekApi.LegacyImport.Mappers;

namespace MobmekApi.Tests.LegacyImport;

public class DocumentMapperTests
{
    [Fact]
    public void LegacyInvoice_CopiesMoneySnapshotsVerbatim_WithConstantGstRate()
    {
        var jobId = Guid.NewGuid();
        // Deliberately inconsistent money fields (tax ≠ 15% of subtotal) — copied, never recomputed.
        var legacy = new LegacyInvoice(7, 3, " Cambelt replacement ", "7 days", " keep an eye on the radiator ",
            LaborPrice: 100m, Discount: 10m, ShippingFee: 5m, SubTotal: 200m, TaxAmount: 44.25m,
            TotalAmount: 339.25m, AmountPaid: 150m, IsPaid: false,
            DueDate: new DateTime(2023, 8, 20), DateAdded: new DateTime(2023, 8, 6, 10, 0, 0), DateEdited: null);

        var invoice = DocumentMapper.Map(legacy, jobId);

        Assert.Equal(jobId, invoice.JobId);
        Assert.Equal("Invoice", invoice.DocumentType);
        Assert.Equal("Cambelt replacement", invoice.IssueName);
        Assert.Equal("keep an eye on the radiator", invoice.Notes);
        Assert.Equal("Active", invoice.Status); // legacy table has no status column
        Assert.Equal(0, invoice.SequenceNumber); // assigned later by the document-sequences phase
        Assert.Equal(100m, invoice.LabourPrice);
        Assert.Equal(10m, invoice.Discount);
        Assert.Equal(5m, invoice.ShippingFee);
        Assert.Equal(200m, invoice.SubTotal);
        Assert.Equal(0.15m, invoice.GstRate);
        Assert.Equal(44.25m, invoice.TaxAmount);
        Assert.Equal(339.25m, invoice.TotalAmount);
        Assert.False(invoice.IsPaid);
        Assert.Equal(150m, invoice.AmountPaid);
        Assert.Equal("7 days", invoice.PaymentTerm);
        Assert.Null(invoice.DatePaid); // legacy Invoice has no DatePaid
        Assert.Equal(new DateOnly(2023, 8, 20), invoice.DueDate);
        // 2023-08-06 10:00 NZST (winter, UTC+12) → 2023-08-05 22:00 UTC.
        Assert.Equal(new DateTime(2023, 8, 5, 22, 0, 0), invoice.CreatedAtUtc);
        Assert.Null(invoice.UpdatedAtUtc);
    }

    [Fact]
    public void LegacyInvoice_NullMoneyFields_CoalesceToZero_ButAmountPaidStaysNull()
    {
        var legacy = new LegacyInvoice(7, 3, "WOF", null, null,
            LaborPrice: null, Discount: null, ShippingFee: null, SubTotal: null, TaxAmount: null,
            TotalAmount: null, AmountPaid: null, IsPaid: false,
            DueDate: null, DateAdded: new DateTime(2023, 8, 6), DateEdited: null);

        var invoice = DocumentMapper.Map(legacy, Guid.NewGuid());

        Assert.Equal(0m, invoice.LabourPrice);
        Assert.Equal(0m, invoice.Discount);
        Assert.Equal(0m, invoice.ShippingFee);
        Assert.Equal(0m, invoice.SubTotal);
        Assert.Equal(0m, invoice.TaxAmount);
        Assert.Equal(0m, invoice.TotalAmount);
        Assert.Null(invoice.AmountPaid);
        Assert.Null(invoice.Notes);
        Assert.Null(invoice.PaymentTerm);
        Assert.Null(invoice.DueDate);
    }

    [Fact]
    public void LegacyQuotation_MapsToQuotationType_NeverPayable()
    {
        var legacy = new LegacyQuotation(12, 3, "Suspension quote", null,
            LaborPrice: 80m, Discount: 0m, ShippingFee: 0m, SubTotal: 500m, TaxAmount: 87m,
            TotalAmount: 667m, DateAdded: new DateTime(2023, 3, 1, 9, 0, 0), DateEdited: null);

        var invoice = DocumentMapper.Map(legacy, Guid.NewGuid());

        Assert.Equal("Quotation", invoice.DocumentType);
        Assert.Equal("Active", invoice.Status);
        Assert.False(invoice.IsPaid);
        Assert.Null(invoice.AmountPaid);
        Assert.Null(invoice.DueDate);
        Assert.Equal(667m, invoice.TotalAmount);
    }

    [Fact]
    public void NewInvoice_CarriesPaymentFieldsAndStatus()
    {
        var legacy = new LegacyNewInvoice(42, 9, "Full service", "On receipt", "Cash + Card (Cash: $250.00, Card: $124.90)",
            null, LabourPrice: 120m, Discount: 0m, ShippingFee: 0m, SubTotal: 300m, TaxAmount: 63m,
            TotalAmount: 483m, AmountPaid: 374.90m, CashAmount: 250m, CardAmount: 124.90m,
            IsPaid: true, Status: "Rejected",
            DueDate: new DateTime(2025, 1, 10), DatePaid: new DateTime(2025, 1, 5, 14, 30, 0),
            DateAdded: new DateTime(2025, 1, 2, 8, 0, 0), DateEdited: new DateTime(2025, 1, 5, 14, 30, 0));

        var invoice = DocumentMapper.Map(legacy, Guid.NewGuid());

        Assert.Equal("Invoice", invoice.DocumentType);
        Assert.Equal("Rejected", invoice.Status); // carried over, not reset to Active
        Assert.True(invoice.IsPaid);
        Assert.Equal(374.90m, invoice.AmountPaid);
        Assert.Equal(250m, invoice.CashAmount);
        Assert.Equal(124.90m, invoice.CardAmount);
        Assert.Equal("On receipt", invoice.PaymentTerm);
        Assert.Equal("Cash + Card (Cash: $250.00, Card: $124.90)", invoice.ModeOfPayment);
        Assert.Equal(new DateOnly(2025, 1, 5), invoice.DatePaid); // Auckland calendar date
        Assert.Equal(new DateOnly(2025, 1, 10), invoice.DueDate);
        Assert.NotNull(invoice.UpdatedAtUtc);
    }

    [Fact]
    public void NewQuotation_ValidUntilBecomesDueDate_AcceptedAppendsMarkerToNotes()
    {
        var legacy = new LegacyNewQuotation(5, 9, "Gearbox rebuild", null, null, "Customer to confirm",
            LabourPrice: 0m, Discount: 0m, ShippingFee: 0m, SubTotal: 2000m, TaxAmount: 300m,
            TotalAmount: 2300m, IsAccepted: true, Status: "Active",
            ValidUntil: new DateTime(2025, 6, 30), DateAdded: new DateTime(2025, 6, 1), DateEdited: null);

        var invoice = DocumentMapper.Map(legacy, Guid.NewGuid());

        Assert.Equal("Quotation", invoice.DocumentType);
        Assert.Equal(new DateOnly(2025, 6, 30), invoice.DueDate);
        Assert.Equal("Customer to confirm\n[Accepted in legacy system]", invoice.Notes);
    }

    [Fact]
    public void NewQuotation_AcceptedWithoutNotes_MarkerStandsAlone()
    {
        var legacy = new LegacyNewQuotation(5, 9, "Gearbox rebuild", null, null, "  ",
            LabourPrice: 0m, Discount: 0m, ShippingFee: 0m, SubTotal: 2000m, TaxAmount: 300m,
            TotalAmount: 2300m, IsAccepted: true, Status: "Active",
            ValidUntil: null, DateAdded: new DateTime(2025, 6, 1), DateEdited: null);

        var invoice = DocumentMapper.Map(legacy, Guid.NewGuid());

        Assert.Equal("[Accepted in legacy system]", invoice.Notes);
        Assert.Null(invoice.DueDate);
    }

    [Fact]
    public void MapItem_NullItemTotal_FallsBackToQuantityTimesPrice()
    {
        var invoiceId = Guid.NewGuid();
        var created = new DateTime(2023, 8, 5, 22, 0, 0);

        var item = DocumentMapper.MapItem(invoiceId, " Oil filter ", 3, 25.50m, itemTotal: null, created);

        Assert.Equal(invoiceId, item.InvoiceId);
        Assert.Equal("Oil filter", item.ItemName);
        Assert.Equal(3, item.Quantity);
        Assert.Equal(25.50m, item.ItemPrice);
        Assert.Equal(76.50m, item.ItemTotal);
        Assert.Equal(created, item.CreatedAtUtc);
    }

    [Fact]
    public void MapItem_StoredItemTotal_CopiedVerbatimEvenWhenInconsistent()
    {
        var item = DocumentMapper.MapItem(Guid.NewGuid(), "Oil filter", 3, 25.50m, itemTotal: 99m, DateTime.UtcNow);

        Assert.Equal(99m, item.ItemTotal);
    }

    [Fact]
    public void MapItem_NameLongerThan255_IsTruncated()
    {
        var longName = new string('x', 1838);

        var item = DocumentMapper.MapItem(Guid.NewGuid(), longName, 1, 10m, 10m, DateTime.UtcNow);

        Assert.Equal(255, item.ItemName.Length);
        Assert.Equal(longName[..255], item.ItemName);
    }
}
