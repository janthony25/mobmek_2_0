using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;
using JobService = MobmekApi.Services.JobService;

namespace MobmekApi.Tests.Services;

public class AppointmentServiceTests
{
    private static readonly DateTime Start = new(2026, 7, 6, 9, 0, 0, DateTimeKind.Utc);

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
            new CreateCarRequest(customer.Id, make.Id, model!.Id, 2020, "ABC123", null, null, null));
        return (customer.Id, car!.Id);
    }

    private static async Task<Guid> SeedEmployeeAsync(AppDbContext db)
    {
        var title = await new EmployeeTitleService(db).CreateAsync(new CreateEmployeeTitleRequest("Mechanic"));
        var type = await new EmploymentTypeService(db).CreateAsync(new CreateEmploymentTypeRequest("Full-time"));
        var (emp, _) = await new EmployeeService(db).CreateAsync(
            new CreateEmployeeRequest("Mac", "Wrench", title.Id, type.Id, "1", "mac@example.com", "addr"));
        return emp!.Id;
    }

    /// <summary>A valid "new caller" booking: soft contact only, no linked records.</summary>
    private static CreateAppointmentRequest NewCallerBooking(DateTime? start = null, DateTime? end = null) =>
        new(
            "Brake inspection",
            start ?? Start,
            end ?? (start ?? Start).AddHours(1),
            AppointmentStatus.Scheduled,
            null,
            "Dave Miller",
            "0215551234",
            "White 2014 Hilux",
            null, null, null, null);

    [Fact]
    public async Task CreateAsync_PersistsSoftContactBooking_WithoutAnyLinks()
    {
        await using var db = CreateContext();
        var service = new AppointmentService(db);

        var (appointment, error) = await service.CreateAsync(NewCallerBooking());

        Assert.Equal(AppointmentWriteError.None, error);
        Assert.NotNull(appointment);
        Assert.Equal("Dave Miller", appointment!.ContactName);
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
        Assert.Null(appointment.CustomerId);
        Assert.Null(appointment.UpdatedAtUtc);
    }

    [Fact]
    public async Task CreateAsync_PersistsLinkedBooking_WithCustomerCarJobAndMechanic()
    {
        await using var db = CreateContext();
        var (customerId, carId) = await SeedCustomerWithCarAsync(db);
        var mechanicId = await SeedEmployeeAsync(db);
        var (job, _) = await new JobService(db).CreateAsync(
            new CreateJobRequest(customerId, carId, "Clutch", JobStatus.AwaitingParts, 0, null, null));
        var service = new AppointmentService(db);

        var (appointment, error) = await service.CreateAsync(NewCallerBooking() with
        {
            ContactName = null,
            ContactPhone = null,
            CustomerId = customerId,
            CarId = carId,
            JobId = job!.Id,
            MechanicId = mechanicId,
        });

        Assert.Equal(AppointmentWriteError.None, error);
        Assert.Equal("Owner Person", appointment!.CustomerName);
        Assert.Equal("Toyota Hilux (ABC123)", appointment.CarDescription);
        Assert.Equal("Clutch", appointment.JobTitle);
        Assert.Equal("Mac Wrench", appointment.MechanicName);
    }

    [Fact]
    public async Task CreateAsync_ReturnsEndNotAfterStart_WhenEndEqualsStart()
    {
        await using var db = CreateContext();
        var service = new AppointmentService(db);

        var (_, error) = await service.CreateAsync(NewCallerBooking(Start, Start));

        Assert.Equal(AppointmentWriteError.EndNotAfterStart, error);
    }

    [Fact]
    public async Task CreateAsync_ReturnsMissingContactOrCustomer_WhenNeitherProvided()
    {
        await using var db = CreateContext();
        var service = new AppointmentService(db);

        var (_, error) = await service.CreateAsync(NewCallerBooking() with
        {
            ContactName = null,
            ContactPhone = null,
        });

        Assert.Equal(AppointmentWriteError.MissingContactOrCustomer, error);
    }

    [Fact]
    public async Task CreateAsync_ReturnsMissingContactOrCustomer_WhenContactNameLacksPhone()
    {
        await using var db = CreateContext();
        var service = new AppointmentService(db);

        var (_, error) = await service.CreateAsync(NewCallerBooking() with { ContactPhone = " " });

        Assert.Equal(AppointmentWriteError.MissingContactOrCustomer, error);
    }

    [Fact]
    public async Task CreateAsync_ReturnsCustomerNotFound_WhenCustomerMissing()
    {
        await using var db = CreateContext();
        var service = new AppointmentService(db);

        var (_, error) = await service.CreateAsync(NewCallerBooking() with { CustomerId = Guid.NewGuid() });

        Assert.Equal(AppointmentWriteError.CustomerNotFound, error);
    }

    [Fact]
    public async Task CreateAsync_ReturnsCarWithoutCustomer_WhenCarLinkedAlone()
    {
        await using var db = CreateContext();
        var (_, carId) = await SeedCustomerWithCarAsync(db);
        var service = new AppointmentService(db);

        var (_, error) = await service.CreateAsync(NewCallerBooking() with { CarId = carId });

        Assert.Equal(AppointmentWriteError.CarWithoutCustomer, error);
    }

    [Fact]
    public async Task CreateAsync_ReturnsCarNotOwnedByCustomer_WhenCarBelongsToAnother()
    {
        await using var db = CreateContext();
        var (customerId, _) = await SeedCustomerWithCarAsync(db);
        var (_, otherCarId) = await SeedCustomerWithCarAsync(db);
        var service = new AppointmentService(db);

        var (_, error) = await service.CreateAsync(NewCallerBooking() with
        {
            CustomerId = customerId,
            CarId = otherCarId,
        });

        Assert.Equal(AppointmentWriteError.CarNotOwnedByCustomer, error);
    }

    [Fact]
    public async Task CreateAsync_ReturnsJobCustomerMismatch_WhenJobBelongsToAnotherCustomer()
    {
        await using var db = CreateContext();
        var (customerId, _) = await SeedCustomerWithCarAsync(db);
        var (otherCustomerId, otherCarId) = await SeedCustomerWithCarAsync(db);
        var (job, _) = await new JobService(db).CreateAsync(
            new CreateJobRequest(otherCustomerId, otherCarId, "Clutch", JobStatus.Open, 0, null, null));
        var service = new AppointmentService(db);

        var (_, error) = await service.CreateAsync(NewCallerBooking() with
        {
            CustomerId = customerId,
            JobId = job!.Id,
        });

        Assert.Equal(AppointmentWriteError.JobCustomerMismatch, error);
    }

    [Fact]
    public async Task CreateAsync_ReturnsJobNotFound_WhenJobMissing()
    {
        await using var db = CreateContext();
        var service = new AppointmentService(db);

        var (_, error) = await service.CreateAsync(NewCallerBooking() with { JobId = Guid.NewGuid() });

        Assert.Equal(AppointmentWriteError.JobNotFound, error);
    }

    [Fact]
    public async Task CreateAsync_ReturnsMechanicNotFound_WhenEmployeeMissing()
    {
        await using var db = CreateContext();
        var service = new AppointmentService(db);

        var (_, error) = await service.CreateAsync(NewCallerBooking() with { MechanicId = Guid.NewGuid() });

        Assert.Equal(AppointmentWriteError.MechanicNotFound, error);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOverlappingRange_OrderedByStart()
    {
        await using var db = CreateContext();
        var service = new AppointmentService(db);

        // One before the window, one inside, one spanning the window's start boundary.
        await service.CreateAsync(NewCallerBooking(Start.AddDays(-2), Start.AddDays(-2).AddHours(1)));
        var (inside, _) = await service.CreateAsync(NewCallerBooking(Start.AddHours(3), Start.AddHours(4)));
        var (spanning, _) = await service.CreateAsync(NewCallerBooking(Start.AddHours(-1), Start.AddHours(1)));

        var results = await service.GetAllAsync(from: Start, to: Start.AddDays(1));

        Assert.Equal(2, results.Count);
        Assert.Equal(spanning!.Id, results[0].Id);
        Assert.Equal(inside!.Id, results[1].Id);
    }

    [Fact]
    public async Task GetAllAsync_UnspecifiedKindRange_IsTreatedAsUtc()
    {
        // Bare query-string dates ("?from=2025-01-01") bind with Kind=Unspecified, which
        // Npgsql rejects for timestamptz — the service must normalize before comparing.
        await using var db = CreateContext();
        var service = new AppointmentService(db);
        await service.CreateAsync(NewCallerBooking(Start.AddDays(-2), Start.AddDays(-2).AddHours(1)));
        var (inside, _) = await service.CreateAsync(NewCallerBooking(Start.AddHours(3), Start.AddHours(4)));

        var results = await service.GetAllAsync(
            from: DateTime.SpecifyKind(Start, DateTimeKind.Unspecified),
            to: DateTime.SpecifyKind(Start.AddDays(1), DateTimeKind.Unspecified));

        Assert.Single(results);
        Assert.Equal(inside!.Id, results[0].Id);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByStatusAndMechanic()
    {
        await using var db = CreateContext();
        var mechanicId = await SeedEmployeeAsync(db);
        var service = new AppointmentService(db);

        await service.CreateAsync(NewCallerBooking());
        var (assigned, _) = await service.CreateAsync(NewCallerBooking(Start.AddHours(2), Start.AddHours(3)) with
        {
            Status = AppointmentStatus.Confirmed,
            MechanicId = mechanicId,
        });

        var byStatus = await service.GetAllAsync(status: AppointmentStatus.Confirmed);
        var byMechanic = await service.GetAllAsync(mechanicId: mechanicId);

        Assert.Single(byStatus);
        Assert.Equal(assigned!.Id, byStatus[0].Id);
        Assert.Single(byMechanic);
        Assert.Equal(assigned.Id, byMechanic[0].Id);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByJobId()
    {
        await using var db = CreateContext();
        var (customerId, carId) = await SeedCustomerWithCarAsync(db);
        var (job, _) = await new JobService(db).CreateAsync(
            new CreateJobRequest(customerId, carId, "Clutch", JobStatus.AwaitingParts, 0, null, null));
        var service = new AppointmentService(db);

        await service.CreateAsync(NewCallerBooking());
        var (linked, _) = await service.CreateAsync(NewCallerBooking(Start.AddHours(2), Start.AddHours(3)) with
        {
            JobId = job!.Id,
        });

        var byJob = await service.GetAllAsync(jobId: job.Id);

        Assert.Single(byJob);
        Assert.Equal(linked!.Id, byJob[0].Id);
    }

    [Fact]
    public async Task GetAllAsync_SearchMatchesCarRego_CustomerName_AndContactFields()
    {
        await using var db = CreateContext();
        var (customerId, carId) = await SeedCustomerWithCarAsync(db); // Owner Person, rego ABC123
        var service = new AppointmentService(db);

        var (linked, _) = await service.CreateAsync(NewCallerBooking() with
        {
            ContactName = null,
            ContactPhone = null,
            VehicleDescription = null,
            CustomerId = customerId,
            CarId = carId,
        });
        var (caller, _) = await service.CreateAsync(NewCallerBooking(Start.AddHours(2), Start.AddHours(3)));

        // Rego and customer name reach the linked booking; case-insensitive.
        var byRego = await service.GetAllAsync(search: "abc123");
        Assert.Single(byRego);
        Assert.Equal(linked!.Id, byRego[0].Id);

        var byCustomerName = await service.GetAllAsync(search: "owner per");
        Assert.Single(byCustomerName);
        Assert.Equal(linked.Id, byCustomerName[0].Id);

        // Contact name and free-text vehicle reach the unconverted caller.
        var byContact = await service.GetAllAsync(search: "dave");
        Assert.Single(byContact);
        Assert.Equal(caller!.Id, byContact[0].Id);

        var byVehicleText = await service.GetAllAsync(search: "hilux");
        Assert.Single(byVehicleText);
        Assert.Equal(caller.Id, byVehicleText[0].Id);

        Assert.Empty(await service.GetAllAsync(search: "no-such-thing"));
    }

    [Fact]
    public async Task UpdateAsync_LinksCustomerAndJob_AndStampsUpdatedAt()
    {
        await using var db = CreateContext();
        var (customerId, carId) = await SeedCustomerWithCarAsync(db);
        var service = new AppointmentService(db);
        var (created, _) = await service.CreateAsync(NewCallerBooking());

        // The convert-on-arrival step: soft contact kept, real records linked in.
        var (updated, error) = await service.UpdateAsync(created!.Id, new UpdateAppointmentRequest(
            created.Title, created.StartUtc, created.EndUtc, AppointmentStatus.Arrived,
            created.Notes, created.ContactName, created.ContactPhone, created.VehicleDescription,
            customerId, carId, null, null));

        Assert.Equal(AppointmentWriteError.None, error);
        Assert.Equal(AppointmentStatus.Arrived, updated!.Status);
        Assert.Equal("Owner Person", updated.CustomerName);
        Assert.Equal("Dave Miller", updated.ContactName);
        Assert.NotNull(updated.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFound_WhenAppointmentMissing()
    {
        await using var db = CreateContext();
        var service = new AppointmentService(db);

        var (_, error) = await service.UpdateAsync(Guid.NewGuid(), new UpdateAppointmentRequest(
            "X", Start, Start.AddHours(1), AppointmentStatus.Scheduled,
            null, "A", "1", null, null, null, null, null));

        Assert.Equal(AppointmentWriteError.NotFound, error);
    }

    [Fact]
    public async Task DeleteAsync_RemovesAppointment_AndReportsMiss()
    {
        await using var db = CreateContext();
        var service = new AppointmentService(db);
        var (created, _) = await service.CreateAsync(NewCallerBooking());

        Assert.True(await service.DeleteAsync(created!.Id));
        Assert.Null(await service.GetByIdAsync(created.Id));
        Assert.False(await service.DeleteAsync(created.Id));
    }
}
