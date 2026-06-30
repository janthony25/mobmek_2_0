using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;
using JobService = MobmekApi.Services.JobService;

namespace MobmekApi.Tests.Services;

public class JobServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static async Task<(Guid CustomerId, Guid CarId)> SeedCustomerWithCarAsync(AppDbContext db)
    {
        var customer = await new CustomerService(db).CreateAsync(
            new CreateCustomerRequest("Owner", "Person", "000", null, null, null));
        var make = await new CarMakeService(db).CreateAsync(new CreateCarMakeRequest("Toyota"));
        var model = await new CarModelService(db).CreateAsync(new CreateCarModelRequest(make.Id, "Hilux"));
        var (car, _) = await new CarService(db).CreateAsync(
            new CreateCarRequest(customer.Id, make.Id, model!.Id, 2020, "ABC123", null, null, null, null));
        return (customer.Id, car!.Id);
    }

    private static CreateJobRequest NewJob(Guid customerId, Guid carId) =>
        new(customerId, carId, "Brake job", JobStatus.Open, 90000, null, null);

    [Fact]
    public async Task CreateAsync_PersistsJob_WhenCarBelongsToCustomer()
    {
        await using var db = CreateContext();
        var (customerId, carId) = await SeedCustomerWithCarAsync(db);
        var service = new JobService(db);

        var (job, error) = await service.CreateAsync(NewJob(customerId, carId));

        Assert.Equal(JobWriteError.None, error);
        Assert.NotNull(job);
        Assert.Equal(JobStatus.Open, job!.Status);
        Assert.Equal(0m, job.TotalJobPrice);
    }

    [Fact]
    public async Task CreateAsync_ReturnsCarNotOwnedByCustomer_WhenCarBelongsToAnother()
    {
        await using var db = CreateContext();
        var (customerId, _) = await SeedCustomerWithCarAsync(db);
        var (_, otherCarId) = await SeedCustomerWithCarAsync(db);
        var service = new JobService(db);

        var (job, error) = await service.CreateAsync(NewJob(customerId, otherCarId));

        Assert.Null(job);
        Assert.Equal(JobWriteError.CarNotOwnedByCustomer, error);
    }

    [Fact]
    public async Task CreateAsync_ReturnsCustomerNotFound_WhenCustomerMissing()
    {
        await using var db = CreateContext();
        var (_, carId) = await SeedCustomerWithCarAsync(db);
        var service = new JobService(db);

        var (_, error) = await service.CreateAsync(NewJob(Guid.NewGuid(), carId));

        Assert.Equal(JobWriteError.CustomerNotFound, error);
    }

    [Fact]
    public async Task AddMechanicAsync_AssignsEmployee_AndPreventsDuplicates()
    {
        await using var db = CreateContext();
        var (customerId, carId) = await SeedCustomerWithCarAsync(db);
        var service = new JobService(db);
        var (job, _) = await service.CreateAsync(NewJob(customerId, carId));

        var title = await new EmployeeTitleService(db).CreateAsync(new CreateEmployeeTitleRequest("Mechanic"));
        var type = await new EmploymentTypeService(db).CreateAsync(new CreateEmploymentTypeRequest("Full-time"));
        var (emp, _) = await new EmployeeService(db).CreateAsync(
            new CreateEmployeeRequest("Mac", "Wrench", title.Id, type.Id, "1", "mac@example.com", "addr"));

        var (withMech, error) = await service.AddMechanicAsync(job!.Id, emp!.Id);
        Assert.Equal(JobWriteError.None, error);
        Assert.Single(withMech!.Mechanics);
        Assert.Equal("Mac Wrench", withMech.Mechanics[0].FullName);

        var (_, dupError) = await service.AddMechanicAsync(job.Id, emp.Id);
        Assert.Equal(JobWriteError.MechanicAlreadyAssigned, dupError);
    }

    [Fact]
    public async Task RecalculateTotalsAsync_SumsItemsLabourAndServices()
    {
        await using var db = CreateContext();
        var (customerId, carId) = await SeedCustomerWithCarAsync(db);
        var service = new JobService(db);
        var (job, _) = await service.CreateAsync(NewJob(customerId, carId));

        // Item: selling 110, trade 100, qty 2 -> total 220, profit 20.
        db.JobItems.Add(new JobItem
        {
            JobId = job!.Id, ItemName = "Pads", TradePrice = 100m, MarkupSolution = MarkupSolution.Dollar,
            Markup = 10m, ItemQuantity = 2, SellingPrice = 110m, UnitProfit = 10m, ItemTotal = 220m,
        });
        db.Labour.Add(new Labour { JobId = job.Id, TotalAmount = 150m });
        db.JobServiceLines.Add(new JobServiceLine { JobId = job.Id, JobServiceId = Guid.NewGuid(), UnitPrice = 89m, Quantity = 1, LineTotal = 89m });
        await db.SaveChangesAsync();

        await service.RecalculateTotalsAsync(job.Id);

        var refreshed = await service.GetByIdAsync(job.Id);
        Assert.Equal(220m + 150m + 89m, refreshed!.TotalJobPrice);
        // profit: item unitProfit*qty (10*2=20) + labour 150 + services 89
        Assert.Equal(20m + 150m + 89m, refreshed.TotalJobProfit);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenMissing()
    {
        await using var db = CreateContext();
        var service = new JobService(db);

        Assert.False(await service.DeleteAsync(Guid.NewGuid()));
    }
}
