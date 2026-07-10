using MobmekApi.Entities;
using MobmekApi.LegacyImport.Mappers;

namespace MobmekApi.Tests.LegacyImport;

public class SyntheticJobBuilderTests
{
    [Theory]
    [InlineData("Invoice", JobStatus.Invoiced)]
    [InlineData("Quotation", JobStatus.Completed)]
    public void Status_FollowsDocumentType(string documentType, JobStatus expected)
    {
        var job = SyntheticJobBuilder.Build(Guid.NewGuid(), Guid.NewGuid(), "WOF", documentType, 7,
            new DateTime(2023, 8, 6), null);

        Assert.Equal(expected, job.Status);
    }

    [Fact]
    public void CarriesDocumentTitleProvenanceNoteAndDates_WithZeroTotals()
    {
        var customerId = Guid.NewGuid();
        var carId = Guid.NewGuid();

        var job = SyntheticJobBuilder.Build(customerId, carId, " Cambelt replacement ", "Invoice", 42,
            new DateTime(2023, 8, 6, 10, 0, 0), new DateTime(2023, 8, 7, 9, 0, 0));

        Assert.Equal(customerId, job.CustomerId);
        Assert.Equal(carId, job.CarId);
        Assert.Equal("Cambelt replacement", job.Title);
        Assert.Equal("Auto-created during legacy import for invoice #42", job.JobNotes);
        Assert.Equal(DiscountType.None, job.DiscountType);
        Assert.Equal(0m, job.TotalJobPrice);
        Assert.Equal(0m, job.TotalJobProfit);
        Assert.Equal(0, job.Odometer);
        // 2023-08-06 10:00 NZST → 2023-08-05 22:00 UTC.
        Assert.Equal(new DateTime(2023, 8, 5, 22, 0, 0), job.CreatedAtUtc);
        Assert.Equal(new DateTime(2023, 8, 6, 21, 0, 0), job.UpdatedAtUtc);
    }

    [Fact]
    public void BlankIssueName_GetsFallbackTitle()
    {
        var job = SyntheticJobBuilder.Build(Guid.NewGuid(), Guid.NewGuid(), "  ", "Quotation", 13,
            new DateTime(2023, 8, 6), null);

        Assert.Equal("Legacy quotation #13", job.Title);
    }

    [Fact]
    public void TitleLongerThan200_IsTruncated()
    {
        var job = SyntheticJobBuilder.Build(Guid.NewGuid(), Guid.NewGuid(), new string('t', 300), "Invoice", 1,
            new DateTime(2023, 8, 6), null);

        Assert.Equal(200, job.Title.Length);
    }
}
