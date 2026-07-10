using MobmekApi.Entities;
using MobmekApi.LegacyImport.Legacy;
using MobmekApi.LegacyImport.Mappers;

namespace MobmekApi.Tests.LegacyImport;

public class AppointmentMapperTests
{
    private static LegacyAppointment Legacy(
        TimeSpan? timeEnd = null,
        string status = "Scheduled",
        string? type = null,
        string? notes = null,
        string? carRego = "ABC123",
        DateTime? dateCancelled = null) => new(
        AppointmentId: 7,
        CustomerName: " Jane Doe ",
        CarRego: carRego,
        CarDetails: "White Hilux 2014",
        Title: " Brake inspection ",
        AppointmentDate: new DateTime(2025, 6, 16), // NZST (winter, UTC+12)
        AppointmentTime: new TimeSpan(10, 30, 0),
        TimeEnd: timeEnd,
        Status: status,
        Type: type,
        Contact: " 0211234567 ",
        Notes: notes,
        CarId: 3,
        GoogleCalendarEventId: "gcal-evt-42",
        DateCreated: new DateTime(2025, 6, 10, 9, 0, 0),
        DateEdited: null,
        DateCancelled: dateCancelled);

    [Fact]
    public void MapsSoftContactFields_HardLinks_AndAucklandTimes()
    {
        var customerId = Guid.NewGuid();
        var carId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        var (appointment, statusFlag, endAdjusted) =
            AppointmentMapper.Map(Legacy(timeEnd: new TimeSpan(11, 30, 0)), customerId, carId, jobId);

        Assert.Equal("Brake inspection", appointment.Title);
        // 2025-06-16 10:30 NZST → 2025-06-15 22:30 UTC.
        Assert.Equal(new DateTime(2025, 6, 15, 22, 30, 0), appointment.StartUtc);
        Assert.Equal(new DateTime(2025, 6, 15, 23, 30, 0), appointment.EndUtc);
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
        Assert.Equal("Jane Doe", appointment.ContactName);
        Assert.Equal("0211234567", appointment.ContactPhone);
        Assert.Equal("White Hilux 2014 (Rego: ABC123)", appointment.VehicleDescription);
        Assert.Equal(customerId, appointment.CustomerId);
        Assert.Equal(carId, appointment.CarId);
        Assert.Equal(jobId, appointment.JobId);
        Assert.Null(appointment.MechanicId);
        Assert.Equal("gcal-evt-42", appointment.GoogleEventId);
        Assert.Equal(new DateTime(2025, 6, 9, 21, 0, 0), appointment.CreatedAtUtc);
        Assert.Null(statusFlag);
        Assert.False(endAdjusted);
    }

    [Fact]
    public void NullTimeEnd_BecomesStartPlusOneHour_WithoutFlag()
    {
        var (appointment, _, endAdjusted) = AppointmentMapper.Map(Legacy(timeEnd: null), null, null, null);

        Assert.Equal(appointment.StartUtc.AddHours(1), appointment.EndUtc);
        Assert.False(endAdjusted); // null is the designed fallback, only bad stored ends are flagged
    }

    [Theory]
    [InlineData(0, 30)] // AM/PM slip: ends before it starts
    [InlineData(10, 30)] // exactly equal
    public void TimeEndNotAfterStart_BecomesStartPlusOneHour_Flagged(int endHour, int endMinute)
    {
        var (appointment, _, endAdjusted) =
            AppointmentMapper.Map(Legacy(timeEnd: new TimeSpan(endHour, endMinute, 0)), null, null, null);

        Assert.Equal(appointment.StartUtc.AddHours(1), appointment.EndUtc);
        Assert.True(endAdjusted);
    }

    [Theory]
    [InlineData("Scheduled", AppointmentStatus.Scheduled, false)]
    [InlineData("In-Progress", AppointmentStatus.Arrived, false)]
    [InlineData("In Progress", AppointmentStatus.Arrived, false)]
    [InlineData("Done", AppointmentStatus.Completed, false)]
    [InlineData("Cancelled", AppointmentStatus.Cancelled, false)]
    [InlineData("Something Odd", AppointmentStatus.Completed, true)]
    [InlineData(null, AppointmentStatus.Completed, true)]
    public void StatusMapping_MatchesFinalizedTable(string? raw, AppointmentStatus expected, bool expectFlag)
    {
        var (status, flagRaw) = AppointmentMapper.MapStatus(raw, isCancelled: false);

        Assert.Equal(expected, status);
        Assert.Equal(expectFlag, flagRaw is not null);
    }

    [Fact]
    public void DateCancelled_OverridesStoredStatus()
    {
        var (appointment, statusFlag, _) = AppointmentMapper.Map(
            Legacy(status: "Scheduled", dateCancelled: new DateTime(2025, 6, 12)), null, null, null);

        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        Assert.Null(statusFlag);
    }

    [Fact]
    public void NotesAndType_Combine_TypeAloneStandsAlone()
    {
        var (withBoth, _, _) = AppointmentMapper.Map(Legacy(type: "Walk In", notes: "Call ahead"), null, null, null);
        var (typeOnly, _, _) = AppointmentMapper.Map(Legacy(type: "Walk In"), null, null, null);
        var (neither, _, _) = AppointmentMapper.Map(Legacy(type: "  "), null, null, null);

        Assert.Equal("Call ahead\n[Type: Walk In]", withBoth.Notes);
        Assert.Equal("[Type: Walk In]", typeOnly.Notes);
        Assert.Null(neither.Notes);
    }

    [Fact]
    public void MissingRego_VehicleDescriptionIsDetailsOnly()
    {
        var (appointment, _, _) = AppointmentMapper.Map(Legacy(carRego: null), null, null, null);

        Assert.Equal("White Hilux 2014", appointment.VehicleDescription);
    }
}
