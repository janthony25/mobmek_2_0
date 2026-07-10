using MobmekApi.Entities;
using MobmekApi.LegacyImport.Legacy;

namespace MobmekApi.LegacyImport.Mappers;

/// <summary>
/// Result of mapping a legacy appointment. <paramref name="StatusFlagRaw"/> is non-null for
/// an unknown legacy status; <paramref name="EndAdjusted"/> is true when a stored TimeEnd
/// not after the start (AM/PM entry errors in the real data) was replaced by start + 1 h.
/// </summary>
public sealed record AppointmentMapResult(Appointment Appointment, string? StatusFlagRaw, bool EndAdjusted);

/// <summary>
/// Legacy Appointment → new Appointment (design §3.6). The old denormalized text fields map
/// onto the new soft-contact fields; the optional CarId becomes hard links (car + its
/// customer); the old Job.AppointmentId back-link becomes the new side's JobId.
/// </summary>
public static class AppointmentMapper
{
    private const int TitleMaxLength = 200;

    private const int VehicleDescriptionMaxLength = 500;

    private const int NotesMaxLength = 4000;

    public static AppointmentMapResult Map(LegacyAppointment legacy, Guid? customerId, Guid? carId, Guid? jobId)
    {
        var (status, statusFlagRaw) = MapStatus(legacy.Status, legacy.DateCancelled is not null);

        var startUtc = NzTime.ToUtc(legacy.AppointmentDate, legacy.AppointmentTime);
        // Null TimeEnd → +1 h (design §3.6); an end at/before the start is an entry error
        // (real data has 23, all AM/PM slips) and gets the same fallback, flagged.
        var endAdjusted = legacy.TimeEnd is not null && legacy.TimeEnd <= legacy.AppointmentTime;
        var endUtc = legacy.TimeEnd is null || endAdjusted
            ? startUtc.AddHours(1)
            : NzTime.ToUtc(legacy.AppointmentDate, legacy.TimeEnd.Value);

        var appointment = new Appointment
        {
            Title = Truncate(legacy.Title.Trim(), TitleMaxLength),
            StartUtc = startUtc,
            EndUtc = endUtc,
            Status = status,
            Notes = BuildNotes(legacy.Notes, legacy.Type),
            ContactName = TrimToNull(legacy.CustomerName),
            ContactPhone = TrimToNull(legacy.Contact),
            VehicleDescription = BuildVehicleDescription(legacy.CarDetails, legacy.CarRego),
            CustomerId = customerId,
            CarId = carId,
            JobId = jobId,
            MechanicId = null,
            GoogleEventId = TrimToNull(legacy.GoogleCalendarEventId),
            CreatedAtUtc = NzTime.ToUtc(legacy.DateCreated),
            UpdatedAtUtc = NzTime.ToUtc(legacy.DateEdited),
        };

        return new AppointmentMapResult(appointment, statusFlagRaw, endAdjusted);
    }

    /// <summary>
    /// Status table finalized from real data (design §3.6). A set DateCancelled overrides
    /// everything; unknown values map to Completed with a flag.
    /// </summary>
    public static (AppointmentStatus Status, string? FlagRaw) MapStatus(string? raw, bool isCancelled)
    {
        if (isCancelled)
        {
            return (AppointmentStatus.Cancelled, null);
        }

        var value = raw?.Trim() ?? string.Empty;
        return value.ToUpperInvariant() switch
        {
            "SCHEDULED" => (AppointmentStatus.Scheduled, null),
            "IN-PROGRESS" or "IN PROGRESS" => (AppointmentStatus.Arrived, null),
            "DONE" => (AppointmentStatus.Completed, null),
            "CANCELLED" => (AppointmentStatus.Cancelled, null),
            _ => (AppointmentStatus.Completed, value.Length == 0 ? "(none)" : value),
        };
    }

    private static string? BuildVehicleDescription(string carDetails, string? carRego)
    {
        var details = carDetails.Trim();
        var rego = carRego?.Trim() ?? string.Empty;
        var combined = (details, rego) switch
        {
            ("", "") => null,
            (_, "") => details,
            ("", _) => $"Rego: {rego}",
            _ => $"{details} (Rego: {rego})",
        };
        return combined is null ? null : Truncate(combined, VehicleDescriptionMaxLength);
    }

    private static string? BuildNotes(string? notes, string? type)
    {
        var body = TrimToNull(notes);
        var typeValue = TrimToNull(type);
        if (typeValue is not null)
        {
            body = body is null ? $"[Type: {typeValue}]" : $"{body}\n[Type: {typeValue}]";
        }

        return body is null ? null : Truncate(body, NotesMaxLength);
    }

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
