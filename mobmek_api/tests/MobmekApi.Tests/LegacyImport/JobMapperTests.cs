using MobmekApi.Entities;
using MobmekApi.LegacyImport.Legacy;
using MobmekApi.LegacyImport.Mappers;

namespace MobmekApi.Tests.LegacyImport;

public class JobMapperTests
{
    private static LegacyJob Legacy(
        string? status = "Done",
        string title = "Brake job",
        string issue = "Squeaky brakes",
        string? notes = null,
        int? odometer = 123456,
        decimal? totalPrice = 500m,
        decimal? totalProfit = 200m,
        DateOnly? started = null,
        DateOnly? finished = null) =>
        new(7, title, issue, notes, status, odometer, 100m, totalProfit, totalPrice,
            started, finished, CarId: 1, MechanicId: null, AppointmentId: null,
            new DateTime(2024, 6, 15, 10, 0, 0), null);

    [Theory]
    [InlineData("Done", JobStatus.Completed, false)]
    [InlineData("In Progress", JobStatus.InProgress, false)]
    [InlineData("Scheduled", JobStatus.Open, false)]
    [InlineData("Waiting for Parts", JobStatus.AwaitingParts, false)]
    [InlineData("Waiting Customer", JobStatus.InProgress, true)]
    [InlineData("Something Odd", JobStatus.Completed, true)]
    [InlineData(null, JobStatus.Completed, true)]
    public void MapStatus_UsesFinalizedTable_AndFlagsUnmappableValues(string? raw, JobStatus expected, bool expectFlag)
    {
        var (status, flagRaw) = JobMapper.MapStatus(raw, hasActiveNewInvoice: false);

        Assert.Equal(expected, status);
        Assert.Equal(expectFlag, flagRaw is not null);
    }

    [Fact]
    public void MapStatus_ActiveNewInvoice_OverridesToInvoiced()
    {
        var (status, flagRaw) = JobMapper.MapStatus("Waiting Customer", hasActiveNewInvoice: true);

        Assert.Equal(JobStatus.Invoiced, status);
        Assert.Null(flagRaw);
    }

    [Fact]
    public void Map_SetsOwnersTitleTotalsAndAuditDates()
    {
        var customerId = Guid.NewGuid();
        var carId = Guid.NewGuid();

        var (job, _) = JobMapper.Map(Legacy(), customerId, carId, [], null, false);

        Assert.Equal(customerId, job.CustomerId);
        Assert.Equal(carId, job.CarId);
        Assert.Equal("Brake job", job.Title);
        Assert.Equal(500m, job.TotalJobPrice);
        Assert.Equal(200m, job.TotalJobProfit);
        Assert.Equal(DiscountType.None, job.DiscountType);
        // 2024-06-15 10:00 NZST → 2024-06-14 22:00 UTC.
        Assert.Equal(new DateTime(2024, 6, 14, 22, 0, 0, DateTimeKind.Utc), job.CreatedAtUtc);
    }

    [Fact]
    public void Map_NullOdometerAndTotals_BecomeZero()
    {
        var (job, _) = JobMapper.Map(
            Legacy(odometer: null, totalPrice: null, totalProfit: null),
            Guid.NewGuid(), Guid.NewGuid(), [], null, false);

        Assert.Equal(0, job.Odometer);
        Assert.Equal(0m, job.TotalJobPrice);
        Assert.Equal(0m, job.TotalJobProfit);
    }

    [Fact]
    public void Map_NotesBlock_PreservesIssueDatesMechanicLabourAndOriginalNotes()
    {
        var legacy = Legacy(
            issue: "Engine light on",
            notes: "Customer will return next week",
            started: new DateOnly(2024, 5, 1),
            finished: new DateOnly(2024, 5, 3));

        var (job, _) = JobMapper.Map(
            legacy, Guid.NewGuid(), Guid.NewGuid(),
            ["Diagnostics", "diagnostics", "Brake replacement", " "],
            "John Smith",
            false);

        Assert.NotNull(job.JobNotes);
        Assert.Contains("Imported from legacy system (Job #7)", job.JobNotes);
        Assert.Contains("Issue: Engine light on", job.JobNotes);
        Assert.Contains("Started: 2024-05-01 · Finished: 2024-05-03", job.JobNotes);
        Assert.Contains("Mechanic: John Smith", job.JobNotes);
        // Labour names deduplicate case-insensitively; blanks dropped.
        Assert.Contains("Labour: Diagnostics; Brake replacement", job.JobNotes);
        Assert.Contains("Customer will return next week", job.JobNotes);
    }

    [Fact]
    public void Map_NotesBlock_OmitsAbsentSections()
    {
        var (job, _) = JobMapper.Map(Legacy(), Guid.NewGuid(), Guid.NewGuid(), [], null, false);

        Assert.NotNull(job.JobNotes);
        Assert.DoesNotContain("Started:", job.JobNotes);
        Assert.DoesNotContain("Mechanic:", job.JobNotes);
        Assert.DoesNotContain("Labour:", job.JobNotes);
    }
}
