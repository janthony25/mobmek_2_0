namespace MobmekApi.Services;

/// <summary>UTC → Pacific/Auckland wall-clock conversion, for displaying a stored UTC instant to
/// NZ-based customers/staff (e.g. an appointment time in an outbound email).</summary>
public static class NzTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");

    public static DateTime FromUtc(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
}
