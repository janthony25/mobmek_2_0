using Google.Apis.Calendar.v3.Data;
using MobmekApi.Entities;

namespace MobmekApi.Services;

/// <summary>
/// Pure mapping from an <see cref="Appointment"/> (with <see cref="Appointment.Customer"/>,
/// <see cref="Appointment.Car"/> and <see cref="Appointment.Mechanic"/> loaded) to a Google
/// Calendar <see cref="Event"/>, per <c>docs/google-calendar-sync-design.md</c> §5. No I/O — safe
/// to unit test directly.
/// </summary>
public static class CalendarEventMapper
{
    private const string AucklandTimeZone = "Pacific/Auckland";

    public static Event Map(Appointment appointment, string frontendBaseUrl)
    {
        return new Event
        {
            Summary = BuildSummary(appointment),
            Description = BuildDescription(appointment, frontendBaseUrl),
            Start = ToEventDateTime(appointment.StartUtc),
            End = ToEventDateTime(appointment.EndUtc),
            ColorId = ColorIdFor(appointment.Status),
        };
    }

    private static string BuildSummary(Appointment appointment)
    {
        var name = ContactOrCustomerName(appointment);
        return name is null ? appointment.Title : $"{name} — {appointment.Title}";
    }

    private static string BuildDescription(Appointment appointment, string frontendBaseUrl)
    {
        var lines = new List<string>
        {
            $"Customer: {ContactOrCustomerName(appointment) ?? "N/A"}",
            $"Phone: {CustomerPhone(appointment) ?? "N/A"}",
            $"Vehicle: {VehicleDescription(appointment) ?? "N/A"}",
        };

        if (appointment.Mechanic is { } mechanic)
        {
            lines.Add($"Mechanic: {mechanic.FirstName} {mechanic.LastName}");
        }

        lines.Add($"Notes: {(string.IsNullOrWhiteSpace(appointment.Notes) ? "N/A" : appointment.Notes)}");
        lines.Add($"{frontendBaseUrl}/appointments");

        return string.Join('\n', lines);
    }

    private static string? ContactOrCustomerName(Appointment appointment) =>
        appointment.Customer is { } customer
            ? $"{customer.FirstName} {customer.LastName}"
            : appointment.ContactName;

    private static string? CustomerPhone(Appointment appointment) =>
        appointment.Customer?.PhoneNumber ?? appointment.ContactPhone;

    private static string? VehicleDescription(Appointment appointment) =>
        appointment.Car is { } car
            ? $"{car.CarMake?.Name} {car.CarModel?.Name} ({car.Rego})"
            : appointment.VehicleDescription;

    private static EventDateTime ToEventDateTime(DateTime utc) => new()
    {
        DateTimeDateTimeOffset = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)),
        TimeZone = AucklandTimeZone,
    };

    /// <summary>Legacy's four colors kept identical (Scheduled/Arrived/Completed/Cancelled),
    /// two new statuses (Confirmed/NoShow) slotted in.</summary>
    private static string ColorIdFor(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Scheduled => "9", // Blue
        AppointmentStatus.Confirmed => "7", // Peacock
        AppointmentStatus.Arrived => "5", // Yellow
        AppointmentStatus.Completed => "10", // Green
        AppointmentStatus.NoShow => "8", // Graphite
        AppointmentStatus.Cancelled => "11", // Red
        _ => "9",
    };
}
