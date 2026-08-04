using MobmekApi.Entities;
using MobmekApi.Services;

namespace MobmekApi.Tests.Services;

/// <summary>
/// Pure mapping tests for <see cref="CalendarEventMapper"/> — no I/O, per
/// <c>docs/google-calendar-sync-design.md</c> §5 and §8.
/// </summary>
public class CalendarEventMapperTests
{
    private static readonly DateTime StartUtc = new(2026, 7, 6, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime EndUtc = new(2026, 7, 6, 10, 0, 0, DateTimeKind.Utc);

    private static Appointment BaseAppointment(AppointmentStatus status = AppointmentStatus.Scheduled) => new()
    {
        Title = "Brake inspection",
        StartUtc = StartUtc,
        EndUtc = EndUtc,
        Status = status,
    };

    [Fact]
    public void Map_WithLinkedCustomer_PrefixesSummaryWithCustomerName()
    {
        var appointment = BaseAppointment();
        appointment.Customer = new Customer { FirstName = "Jane", LastName = "Doe", PhoneNumber = "111" };

        var googleEvent = CalendarEventMapper.Map(appointment, "http://localhost:3000");

        Assert.Equal("Jane Doe — Brake inspection", googleEvent.Summary);
    }

    [Fact]
    public void Map_WithSoftContactOnly_PrefixesSummaryWithContactName()
    {
        var appointment = BaseAppointment();
        appointment.ContactName = "Dave Miller";

        var googleEvent = CalendarEventMapper.Map(appointment, "http://localhost:3000");

        Assert.Equal("Dave Miller — Brake inspection", googleEvent.Summary);
    }

    [Fact]
    public void Map_WithNeitherCustomerNorContactName_UsesTitleAlone()
    {
        var appointment = BaseAppointment();

        var googleEvent = CalendarEventMapper.Map(appointment, "http://localhost:3000");

        Assert.Equal("Brake inspection", googleEvent.Summary);
    }

    [Fact]
    public void Map_WithLinkedCar_DescribesVehicleFromCarMakeModelRego()
    {
        var appointment = BaseAppointment();
        appointment.Car = new Car
        {
            CarMakeId = Guid.NewGuid(),
            CarModelId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            Rego = "ABC123",
            CarMake = new CarMake { Name = "Toyota" },
            CarModel = new CarModel { Name = "Hilux", CarMakeId = Guid.NewGuid() },
        };

        var googleEvent = CalendarEventMapper.Map(appointment, "http://localhost:3000");

        Assert.Contains("Vehicle: Toyota Hilux (ABC123)", googleEvent.Description);
    }

    [Fact]
    public void Map_WithoutLinkedCar_FallsBackToVehicleDescription()
    {
        var appointment = BaseAppointment();
        appointment.VehicleDescription = "White 2014 Hilux";

        var googleEvent = CalendarEventMapper.Map(appointment, "http://localhost:3000");

        Assert.Contains("Vehicle: White 2014 Hilux", googleEvent.Description);
    }

    [Fact]
    public void Map_IncludesFrontendLink_InDescription()
    {
        var appointment = BaseAppointment();

        var googleEvent = CalendarEventMapper.Map(appointment, "https://app.example.com");

        Assert.Contains("https://app.example.com/appointments", googleEvent.Description);
    }

    [Fact]
    public void Map_WithMechanic_IncludesMechanicLine()
    {
        var appointment = BaseAppointment();
        appointment.Mechanic = new Employee
        {
            FirstName = "Mac",
            LastName = "Wrench",
            TitleId = Guid.NewGuid(),
            EmploymentTypeId = Guid.NewGuid(),
            ContactNumber = "1",
            EmailAddress = "mac@example.com",
            PhysicalAddress = "addr",
        };

        var googleEvent = CalendarEventMapper.Map(appointment, "http://localhost:3000");

        Assert.Contains("Mechanic: Mac Wrench", googleEvent.Description);
    }

    [Theory]
    [InlineData(AppointmentStatus.Scheduled, "9")]
    [InlineData(AppointmentStatus.Confirmed, "7")]
    [InlineData(AppointmentStatus.Arrived, "5")]
    [InlineData(AppointmentStatus.Completed, "10")]
    [InlineData(AppointmentStatus.NoShow, "8")]
    [InlineData(AppointmentStatus.Cancelled, "11")]
    public void Map_SetsColorIdForEachStatus(AppointmentStatus status, string expectedColorId)
    {
        var appointment = BaseAppointment(status);

        var googleEvent = CalendarEventMapper.Map(appointment, "http://localhost:3000");

        Assert.Equal(expectedColorId, googleEvent.ColorId);
    }

    [Fact]
    public void Map_SetsUtcInstantsWithAucklandTimeZone()
    {
        var appointment = BaseAppointment();

        var googleEvent = CalendarEventMapper.Map(appointment, "http://localhost:3000");

        Assert.Equal(new DateTimeOffset(StartUtc), googleEvent.Start.DateTimeDateTimeOffset);
        Assert.Equal("Pacific/Auckland", googleEvent.Start.TimeZone);
        Assert.Equal(new DateTimeOffset(EndUtc), googleEvent.End.DateTimeDateTimeOffset);
        Assert.Equal("Pacific/Auckland", googleEvent.End.TimeZone);
    }
}
