using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;
using JobService = MobmekApi.Services.JobService;

namespace MobmekApi.Tests.Services;

public class JobServiceLineServiceTests
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
    public async Task CreateAsync_SnapshotsCatalogPrice_AndComputesLineTotal()
    {
        await using var db = CreateContext();
        var (jobs, jobId) = await SeedJobAsync(db);
        var catalog = await new JobServiceCatalogService(db).CreateAsync(new CreateJobServiceRequest("Oil change", null, 89m, true));
        var service = new JobServiceLineService(db, jobs);

        var (line, error) = await service.CreateAsync(jobId, new CreateJobServiceLineRequest(catalog.Id, 2));

        Assert.Equal(JobServiceLineWriteError.None, error);
        Assert.Equal(89m, line!.UnitPrice);
        Assert.Equal(178m, line.LineTotal);
        Assert.Equal("Oil change", line.ServiceName);
    }

    [Fact]
    public async Task CreateAsync_SnapshotIsStable_WhenCatalogPriceChangesLater()
    {
        await using var db = CreateContext();
        var (jobs, jobId) = await SeedJobAsync(db);
        var catalogService = new JobServiceCatalogService(db);
        var catalog = await catalogService.CreateAsync(new CreateJobServiceRequest("General service", null, 200m, true));
        var service = new JobServiceLineService(db, jobs);

        var (line, _) = await service.CreateAsync(jobId, new CreateJobServiceLineRequest(catalog.Id, 1));
        await catalogService.UpdateAsync(catalog.Id, new UpdateJobServiceRequest("General service", null, 999m, true));

        var refetched = await service.GetByIdAsync(jobId, line!.Id);
        Assert.Equal(200m, refetched!.UnitPrice); // unchanged by later catalog edit
    }

    [Fact]
    public async Task CreateAsync_ReturnsServiceInactive_WhenCatalogServiceInactive()
    {
        await using var db = CreateContext();
        var (jobs, jobId) = await SeedJobAsync(db);
        var catalog = await new JobServiceCatalogService(db).CreateAsync(new CreateJobServiceRequest("Retired", null, 10m, false));
        var service = new JobServiceLineService(db, jobs);

        var (line, error) = await service.CreateAsync(jobId, new CreateJobServiceLineRequest(catalog.Id, 1));

        Assert.Null(line);
        Assert.Equal(JobServiceLineWriteError.ServiceInactive, error);
    }

    [Fact]
    public async Task CreateAsync_ReturnsJobNotFound_WhenJobMissing()
    {
        await using var db = CreateContext();
        var catalog = await new JobServiceCatalogService(db).CreateAsync(new CreateJobServiceRequest("Oil", null, 10m, true));
        var service = new JobServiceLineService(db, new JobService(db));

        var (_, error) = await service.CreateAsync(Guid.NewGuid(), new CreateJobServiceLineRequest(catalog.Id, 1));

        Assert.Equal(JobServiceLineWriteError.JobNotFound, error);
    }

    [Fact]
    public async Task CreateAsync_UpdatesParentJobTotals()
    {
        await using var db = CreateContext();
        var (jobs, jobId) = await SeedJobAsync(db);
        var catalog = await new JobServiceCatalogService(db).CreateAsync(new CreateJobServiceRequest("Oil", null, 50m, true));
        var service = new JobServiceLineService(db, jobs);

        await service.CreateAsync(jobId, new CreateJobServiceLineRequest(catalog.Id, 3)); // 150

        var job = await jobs.GetByIdAsync(jobId);
        Assert.Equal(150m, job!.TotalJobPrice);
        Assert.Equal(150m, job.TotalJobProfit); // services 100% profit
    }

    [Fact]
    public async Task DeleteAsync_RemovesLine_AndRecalculatesTotals()
    {
        await using var db = CreateContext();
        var (jobs, jobId) = await SeedJobAsync(db);
        var catalog = await new JobServiceCatalogService(db).CreateAsync(new CreateJobServiceRequest("Oil", null, 50m, true));
        var service = new JobServiceLineService(db, jobs);
        var (line, _) = await service.CreateAsync(jobId, new CreateJobServiceLineRequest(catalog.Id, 1));

        Assert.True(await service.DeleteAsync(jobId, line!.Id));

        var job = await jobs.GetByIdAsync(jobId);
        Assert.Equal(0m, job!.TotalJobPrice);
    }
}
