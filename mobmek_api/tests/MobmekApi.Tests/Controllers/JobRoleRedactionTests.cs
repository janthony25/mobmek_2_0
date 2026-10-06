using MobmekApi.Controllers;
using MobmekApi.DTOs;
using MobmekApi.Entities;

namespace MobmekApi.Tests.Controllers;

/// <summary>
/// No "Employee" account exists yet, but the role is already assignable (AccountAdminService),
/// and JobDto/JobItemDto previously carried cost/margin fields to every authenticated caller
/// with nothing to stop it. These tests pin down that an Admin caller is unaffected and a
/// non-Admin caller never sees TotalJobProfit / TradePrice / Markup / UnitProfit.
/// </summary>
public class JobRoleRedactionTests
{
    private static JobDto SampleJob(decimal? totalJobProfit = 123.45m) => new(
        Id: Guid.NewGuid(),
        CustomerId: Guid.NewGuid(),
        CustomerName: "Jane Doe",
        CarId: Guid.NewGuid(),
        CarDescription: "2020 Toyota Corolla",
        Title: "Brake service",
        Status: JobStatus.Open,
        Odometer: 50000,
        JobNotes: null,
        InvoiceNotes: null,
        DiscountType: DiscountType.None,
        DiscountValue: 0m,
        TotalJobPrice: 500m,
        TotalJobProfit: totalJobProfit,
        Mechanics: [],
        CreatedAtUtc: DateTime.UtcNow,
        UpdatedAtUtc: null,
        UpdatedByName: null);

    private static JobItemDto SampleItem(decimal? tradePrice = 100m, decimal? markup = 15m, decimal? unitProfit = 107m) => new(
        Id: Guid.NewGuid(),
        JobId: Guid.NewGuid(),
        ItemName: "Brake pads",
        TradePrice: tradePrice,
        RetailPrice: 180m,
        MarkupSolution: MarkupSolution.Percentage,
        Markup: markup,
        ItemQuantity: 1,
        SellingPrice: 207m,
        UnitProfit: unitProfit,
        ItemTotal: 207m,
        CreatedAtUtc: DateTime.UtcNow,
        UpdatedAtUtc: null);

    [Fact]
    public void Redact_Job_LeavesAdminUntouched()
    {
        var job = SampleJob();

        var result = job.Redact(canViewMargins: true);

        Assert.Equal(job, result);
        Assert.Equal(123.45m, result.TotalJobProfit);
    }

    [Fact]
    public void Redact_Job_HidesProfitFromNonAdmin()
    {
        var result = SampleJob().Redact(canViewMargins: false);

        Assert.Null(result.TotalJobProfit);
    }

    [Fact]
    public void Redact_Job_NonAdminKeepsEverythingElse()
    {
        var job = SampleJob();

        var result = job.Redact(canViewMargins: false);

        Assert.Equal(job.Id, result.Id);
        Assert.Equal(job.TotalJobPrice, result.TotalJobPrice);
        Assert.Equal(job.Title, result.Title);
    }

    [Fact]
    public void Redact_JobList_AppliesToEveryItem()
    {
        IReadOnlyList<JobDto> jobs = [SampleJob(), SampleJob()];

        var result = jobs.Redact(canViewMargins: false);

        Assert.All(result, j => Assert.Null(j.TotalJobProfit));
    }

    [Fact]
    public void Redact_PagedJobs_RedactsItemsButKeepsPagingMetadata()
    {
        var page = new PagedResult<JobDto>([SampleJob()], TotalCount: 42, Page: 2, PageSize: 15);

        var result = page.Redact(canViewMargins: false);

        Assert.Null(result.Items[0].TotalJobProfit);
        Assert.Equal(42, result.TotalCount);
        Assert.Equal(2, result.Page);
    }

    [Fact]
    public void Redact_JobItem_LeavesAdminUntouched()
    {
        var item = SampleItem();

        var result = item.Redact(canViewMargins: true);

        Assert.Equal(item, result);
    }

    [Fact]
    public void Redact_JobItem_HidesTradePriceMarkupAndUnitProfitFromNonAdmin()
    {
        var result = SampleItem().Redact(canViewMargins: false);

        Assert.Null(result.TradePrice);
        Assert.Null(result.Markup);
        Assert.Null(result.UnitProfit);
    }

    [Fact]
    public void Redact_JobItem_NonAdminKeepsSellingPriceAndRetailPrice()
    {
        var item = SampleItem();

        var result = item.Redact(canViewMargins: false);

        // Retail/selling price are what the customer sees on the invoice — not a margin leak,
        // and a non-Admin tech still needs them to do the job.
        Assert.Equal(item.RetailPrice, result.RetailPrice);
        Assert.Equal(item.SellingPrice, result.SellingPrice);
        Assert.Equal(item.ItemTotal, result.ItemTotal);
    }

    [Fact]
    public void Redact_JobItemList_AppliesToEveryItem()
    {
        IReadOnlyList<JobItemDto> items = [SampleItem(), SampleItem()];

        var result = items.Redact(canViewMargins: false);

        Assert.All(result, i => Assert.Null(i.UnitProfit));
    }
}
