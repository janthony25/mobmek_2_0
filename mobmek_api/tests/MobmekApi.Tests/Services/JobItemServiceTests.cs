using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;
using JobService = MobmekApi.Services.JobService;

namespace MobmekApi.Tests.Services;

public class JobItemServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static async Task<(JobService Jobs, Guid JobId)> SeedJobAsync(AppDbContext db)
    {
        var customer = await new CustomerService(db).CreateAsync(new CreateCustomerRequest("O", "P", "0", null, null, null));
        var make = await new CarMakeService(db).CreateAsync(new CreateCarMakeRequest("Make"));
        var model = await new CarModelService(db).CreateAsync(new CreateCarModelRequest(make.Id, "Model"));
        var (car, _) = await new CarService(db).CreateAsync(new CreateCarRequest(customer.Id, make.Id, model!.Id, 2020, "R", null, null, null));
        var jobs = new JobService(db);
        var (job, _) = await jobs.CreateAsync(new CreateJobRequest(customer.Id, car!.Id, "Job", JobStatus.Open, 1000, null, null));
        return (jobs, job!.Id);
    }

    [Fact]
    public async Task CreateAsync_PercentageMarkup_ComputesSellingProfitTotal()
    {
        await using var db = CreateContext();
        var (_, jobId) = await SeedJobAsync(db);
        var service = new JobItemService(db, new JobService(db));

        var item = await service.CreateAsync(jobId, new CreateJobItemRequest(
            "Brake pads", TradePrice: 100m, RetailPrice: 180m, MarkupSolution.Percentage, Markup: 15m, ItemQuantity: 2, SellingPrice: null));

        Assert.NotNull(item);
        Assert.Equal(207m, item!.SellingPrice);   // 180 * 1.15 (markup on retail)
        Assert.Equal(107m, item.UnitProfit);       // 207 - 100 (vs trade cost)
        Assert.Equal(414m, item.ItemTotal);        // 207 * 2
    }

    [Fact]
    public async Task CreateAsync_DollarMarkup_ComputesSellingPrice()
    {
        await using var db = CreateContext();
        var (_, jobId) = await SeedJobAsync(db);
        var service = new JobItemService(db, new JobService(db));

        var item = await service.CreateAsync(jobId, new CreateJobItemRequest(
            "Filter", TradePrice: 20m, RetailPrice: 30m, MarkupSolution.Dollar, Markup: 15m, ItemQuantity: 1, SellingPrice: null));

        Assert.Equal(45m, item!.SellingPrice);     // 30 + 15 (markup on retail)
        Assert.Equal(25m, item.UnitProfit);        // 45 - 20 (vs trade cost)
        Assert.Equal(45m, item.ItemTotal);
    }

    [Fact]
    public async Task CreateAsync_NoRetailPrice_UsesManualSellingPrice_AndFullProfit()
    {
        await using var db = CreateContext();
        var (_, jobId) = await SeedJobAsync(db);
        var service = new JobItemService(db, new JobService(db));

        var item = await service.CreateAsync(jobId, new CreateJobItemRequest(
            "Misc", TradePrice: null, RetailPrice: null, MarkupSolution.Percentage, Markup: 0m, ItemQuantity: 3, SellingPrice: 50m));

        Assert.Equal(50m, item!.SellingPrice);
        Assert.Equal(50m, item.UnitProfit);        // no trade price -> all profit
        Assert.Equal(150m, item.ItemTotal);
    }

    [Fact]
    public async Task CreateAsync_ReturnsNull_WhenJobMissing()
    {
        await using var db = CreateContext();
        var service = new JobItemService(db, new JobService(db));

        var item = await service.CreateAsync(Guid.NewGuid(), new CreateJobItemRequest(
            "X", 1m, null, MarkupSolution.Dollar, 0m, 1, null));

        Assert.Null(item);
    }

    [Fact]
    public async Task CreateAsync_UpdatesParentJobTotals()
    {
        await using var db = CreateContext();
        var (jobs, jobId) = await SeedJobAsync(db);
        var service = new JobItemService(db, jobs);

        await service.CreateAsync(jobId, new CreateJobItemRequest(
            "Pads", 100m, 100m, MarkupSolution.Dollar, 10m, 2, null)); // selling 110, total 220, profit 20

        var job = await jobs.GetByIdAsync(jobId);
        Assert.Equal(220m, job!.TotalJobPrice);
        Assert.Equal(20m, job.TotalJobProfit);
    }

    [Fact]
    public async Task DeleteAsync_RemovesItem_AndRecalculatesTotals()
    {
        await using var db = CreateContext();
        var (jobs, jobId) = await SeedJobAsync(db);
        var service = new JobItemService(db, jobs);
        var item = await service.CreateAsync(jobId, new CreateJobItemRequest(
            "Pads", 100m, 100m, MarkupSolution.Dollar, 10m, 1, null));

        Assert.True(await service.DeleteAsync(jobId, item!.Id));

        var job = await jobs.GetByIdAsync(jobId);
        Assert.Equal(0m, job!.TotalJobPrice);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenItemBelongsToAnotherJob()
    {
        await using var db = CreateContext();
        var (jobs, jobId) = await SeedJobAsync(db);
        var seededJob = await db.Jobs.AsNoTracking().FirstAsync();
        var (otherJob, _) = await jobs.CreateAsync(new CreateJobRequest(
            seededJob.CustomerId, seededJob.CarId, "Other", JobStatus.Open, 0, null, null));
        var service = new JobItemService(db, jobs);
        var item = await service.CreateAsync(jobId, new CreateJobItemRequest(
            "Pads", 100m, null, MarkupSolution.Dollar, 10m, 1, null));

        // Same item id, wrong parent job -> not found.
        Assert.Null(await service.GetByIdAsync(otherJob!.Id, item!.Id));
        Assert.False(await service.DeleteAsync(otherJob.Id, item.Id));
        Assert.NotNull(await service.GetByIdAsync(jobId, item.Id));
    }
}
