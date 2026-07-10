using MobmekApi.Entities;
using MobmekApi.LegacyImport.Mappers;

namespace MobmekApi.Tests.LegacyImport;

public class SequenceNumberAssignerTests
{
    private static Invoice Doc(string type, DateTime createdUtc) => new()
    {
        IssueName = "x",
        DocumentType = type,
        SequenceNumber = 0,
        CreatedAtUtc = createdUtc,
    };

    [Fact]
    public void OrdersByCreatedAt_PerType_StartingAtCurrentMaxPlusOne()
    {
        var oldestInvoice = Doc("Invoice", new DateTime(2020, 1, 1));
        var newestInvoice = Doc("Invoice", new DateTime(2024, 1, 1));
        var middleInvoice = Doc("Invoice", new DateTime(2022, 1, 1));
        var quotation = Doc("Quotation", new DateTime(2021, 1, 1));
        // Existing numbered documents: invoices up to 16, quotations up to 5.
        var maxByType = new Dictionary<string, int> { ["Invoice"] = 16, ["Quotation"] = 5 };

        var assignments = SequenceNumberAssigner.Assign(
            [newestInvoice, quotation, oldestInvoice, middleInvoice],
            type => maxByType[type]);

        Assert.Equal(4, assignments.Count);
        Assert.Equal(17, assignments.Single(a => a.Invoice == oldestInvoice).Number);
        Assert.Equal(18, assignments.Single(a => a.Invoice == middleInvoice).Number);
        Assert.Equal(19, assignments.Single(a => a.Invoice == newestInvoice).Number);
        Assert.Equal(6, assignments.Single(a => a.Invoice == quotation).Number);
    }

    [Fact]
    public void EmptyTargetDatabase_NumbersStartAtOne()
    {
        var first = Doc("Invoice", new DateTime(2020, 1, 1));
        var second = Doc("Invoice", new DateTime(2020, 2, 1));

        var assignments = SequenceNumberAssigner.Assign([second, first], _ => 0);

        Assert.Equal(1, assignments.Single(a => a.Invoice == first).Number);
        Assert.Equal(2, assignments.Single(a => a.Invoice == second).Number);
    }

    [Fact]
    public void EqualTimestamps_TieBreakIsDeterministic()
    {
        var when = new DateTime(2020, 1, 1);
        var a = Doc("Invoice", when);
        var b = Doc("Invoice", when);

        var first = SequenceNumberAssigner.Assign([a, b], _ => 0);
        var second = SequenceNumberAssigner.Assign([b, a], _ => 0);

        Assert.Equal(
            first.Select(x => (x.Invoice.Id, x.Number)),
            second.Select(x => (x.Invoice.Id, x.Number)));
    }
}
