namespace MobmekApi.LegacyImport;

/// <summary>
/// Converts legacy timestamps to UTC. The old system stored <c>DateTime.Now</c> from a
/// server running Pacific/Auckland wall-clock time; the new system stores UTC
/// (docs/legacy-import-design.md §1.4).
/// </summary>
public static class NzTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");

    /// <summary>
    /// Auckland wall-clock → UTC. Times inside the spring-forward gap (which never really
    /// occurred on a clock) are shifted forward one hour; ambiguous times at the fall-back
    /// resolve to NZ standard time, matching <see cref="TimeZoneInfo.ConvertTimeToUtc(DateTime, TimeZoneInfo)"/>.
    /// </summary>
    public static DateTime ToUtc(DateTime aucklandWallClock)
    {
        var local = DateTime.SpecifyKind(aucklandWallClock, DateTimeKind.Unspecified);
        if (Zone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, Zone);
    }

    public static DateTime? ToUtc(DateTime? aucklandWallClock) =>
        aucklandWallClock is null ? null : ToUtc(aucklandWallClock.Value);

    /// <summary>Auckland date + time-of-day (legacy split columns) → UTC instant.</summary>
    public static DateTime ToUtc(DateTime aucklandDate, TimeSpan timeOfDay) =>
        ToUtc(aucklandDate.Date + timeOfDay);

    /// <summary>The Auckland calendar date of a legacy timestamp (for DateOnly targets like DueDate).</summary>
    public static DateOnly ToDateOnly(DateTime aucklandWallClock) => DateOnly.FromDateTime(aucklandWallClock);

    public static DateOnly? ToDateOnly(DateTime? aucklandWallClock) =>
        aucklandWallClock is null ? null : DateOnly.FromDateTime(aucklandWallClock.Value);
}
