using MobmekApi.Entities;
using MobmekApi.LegacyImport.Legacy;
using MobmekApi.LegacyImport.Mappers;

namespace MobmekApi.Tests.LegacyImport;

public class JobChildMapperTests
{
    [Theory]
    [InlineData("%", MarkupSolution.Percentage)]
    [InlineData("$", MarkupSolution.Dollar)]
    [InlineData(" $ ", MarkupSolution.Dollar)]
    public void JobItem_MarkupSolutionSymbol_MapsToEnum(string symbol, MarkupSolution expected)
    {
        var legacy = new LegacyJobItem(1, 7, "Brake pads", 50m, 80m, symbol, 20m, 96m, 46m, 2, 192m,
            new DateTime(2024, 6, 15, 10, 0, 0), null);

        var item = JobItemMapper.Map(legacy, Guid.NewGuid());

        Assert.Equal(expected, item.MarkupSolution);
    }

    [Fact]
    public void JobItem_CopiesStoredComputedFieldsVerbatim()
    {
        var jobId = Guid.NewGuid();
        // Deliberately inconsistent numbers (96 × 2 ≠ 200) — the mapper must not recompute.
        var legacy = new LegacyJobItem(1, 7, " Brake pads ", 50m, 80m, "%", 20m, 96m, 46m, 2, 200m,
            new DateTime(2024, 6, 15, 10, 0, 0), null);

        var item = JobItemMapper.Map(legacy, jobId);

        Assert.Equal(jobId, item.JobId);
        Assert.Equal("Brake pads", item.ItemName);
        Assert.Equal(50m, item.TradePrice);
        Assert.Equal(80m, item.RetailPrice);
        Assert.Equal(20m, item.Markup);
        Assert.Equal(2, item.ItemQuantity);
        Assert.Equal(96m, item.SellingPrice);
        Assert.Equal(46m, item.UnitProfit);
        Assert.Equal(200m, item.ItemTotal);
    }

    [Fact]
    public void Labour_MapsHoursRateAndTotal_WithNoFixedAmount()
    {
        var jobId = Guid.NewGuid();
        // 2.5 h × 90 stored total 300 — copied verbatim, not recomputed.
        var legacy = new LegacyLabour(1, 7, "Diagnostics", 2.5m, 90m, 300m,
            new DateTime(2024, 6, 15, 10, 0, 0), null);

        var labour = LabourMapper.Map(legacy, jobId);

        Assert.Equal(jobId, labour.JobId);
        Assert.Equal(2.5m, labour.Hours);
        Assert.Equal(90m, labour.RatePerHour);
        Assert.Null(labour.FixedAmount);
        Assert.Equal(300m, labour.TotalAmount);
    }

    [Fact]
    public void ServiceLine_UnitPriceIsCatalogPricePlusAdditional_QuantityOne()
    {
        var jobId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var legacy = new LegacyJobServiceJoin(1, 7, 3, AdditionalAmount: 25m, new DateTime(2024, 6, 15, 10, 0, 0));

        var line = ServiceLineMapper.Map(legacy, jobId, serviceId, catalogPrice: 120m);

        Assert.Equal(jobId, line.JobId);
        Assert.Equal(serviceId, line.JobServiceId);
        Assert.Equal(145m, line.UnitPrice);
        Assert.Equal(1, line.Quantity);
        Assert.Equal(145m, line.LineTotal);
    }
}
