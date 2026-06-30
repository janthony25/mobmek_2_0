using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;
using JobService = MobmekApi.Services.JobService;

namespace MobmekApi.Tests.Services;

public class LabourServiceTests
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
        var (car, _) = await new CarService(db).CreateAsync(new CreateCarRequest(customer.Id, make.Id, model!.Id, 2020, "R", null, null, null, null));
        var jobs = new JobService(db);
        var (job, _) = await jobs.CreateAsync(new CreateJobRequest(customer.Id, car!.Id, "Job", JobStatus.Open, 1000, null, null));
        return (jobs, job!.Id);
    }

    [Fact]
    public async Task CreateAsync_HoursTimesRate_ComputesTotal()
    {
        await using var db = CreateContext();
        var (_, jobId) = await SeedJobAsync(db);
        var service = new LabourService(db, new JobService(db));

        var labour = await service.CreateAsync(jobId, new CreateLabourRequest(Hours: 2.5m, RatePerHour: 120m, FixedAmount: null));

        Assert.Equal(300m, labour!.TotalAmount);
    }

    [Fact]
    public async Task CreateAsync_FixedAmount_OverridesHoursAndRate()
    {
        await using var db = CreateContext();
        var (_, jobId) = await SeedJobAsync(db);
        var service = new LabourService(db, new JobService(db));

        var labour = await service.CreateAsync(jobId, new CreateLabourRequest(Hours: null, RatePerHour: null, FixedAmount: 500m));

        Assert.Equal(500m, labour!.TotalAmount);
    }

    [Fact]
    public async Task CreateAsync_ReturnsNull_WhenJobMissing()
    {
        await using var db = CreateContext();
        var service = new LabourService(db, new JobService(db));

        var labour = await service.CreateAsync(Guid.NewGuid(), new CreateLabourRequest(1m, 1m, null));

        Assert.Null(labour);
    }

    [Fact]
    public async Task CreateAsync_UpdatesParentJobTotals()
    {
        await using var db = CreateContext();
        var (jobs, jobId) = await SeedJobAsync(db);
        var service = new LabourService(db, jobs);

        await service.CreateAsync(jobId, new CreateLabourRequest(2m, 100m, null)); // 200

        var job = await jobs.GetByIdAsync(jobId);
        Assert.Equal(200m, job!.TotalJobPrice);
        Assert.Equal(200m, job.TotalJobProfit); // labour is 100% profit
    }

    [Fact]
    public async Task DeleteAsync_RemovesLabour_AndRecalculatesTotals()
    {
        await using var db = CreateContext();
        var (jobs, jobId) = await SeedJobAsync(db);
        var service = new LabourService(db, jobs);
        var labour = await service.CreateAsync(jobId, new CreateLabourRequest(1m, 100m, null));

        Assert.True(await service.DeleteAsync(jobId, labour!.Id));

        var job = await jobs.GetByIdAsync(jobId);
        Assert.Equal(0m, job!.TotalJobPrice);
    }
}
