namespace MobmekApi.Data;

/// <summary>
/// Pure schedule-expansion math shared by <see cref="Services.RecurringTransactionService"/>
/// (due-occurrence queue) and <see cref="Services.ForecastService"/> (forecast horizon) — kept
/// framework-free and static so both can call it without a DB round trip and so it's trivial to
/// unit test in isolation.
/// </summary>
public static class RecurringOccurrences
{
    public static readonly string[] ValidFrequencies = ["Weekly", "Fortnightly", "Monthly", "Quarterly", "Annually"];

    /// <summary>
    /// Occurrence dates in <c>[from, to]</c> (inclusive both ends), starting at
    /// <paramref name="anchorDate"/> and stepping by <paramref name="frequency"/> ×
    /// <paramref name="interval"/>, stopping at <paramref name="endDate"/> if set. Monthly-family
    /// steps use <see cref="DateOnly.AddMonths"/>/<see cref="DateOnly.AddYears"/> so a Jan-31
    /// anchor naturally clamps to the 28th/30th in shorter months rather than overflowing.
    /// </summary>
    public static IEnumerable<DateOnly> Expand(
        string frequency, int interval, DateOnly anchorDate, DateOnly? endDate, DateOnly from, DateOnly to)
    {
        // Each occurrence is computed straight from the anchor (not by stepping off the previous
        // occurrence), so a month-length clamp (e.g. Jan 31 -> Feb 28) never compounds into later
        // occurrences the way repeatedly calling AddMonths on the clamped date would.
        for (var period = 0; ; period++)
        {
            var date = AddPeriods(anchorDate, frequency, interval, period);
            if (date > to || (endDate is not null && date > endDate))
            {
                yield break;
            }

            if (date >= from)
            {
                yield return date;
            }
        }
    }

    private static DateOnly AddPeriods(DateOnly anchor, string frequency, int interval, int periods) => frequency switch
    {
        "Weekly" => anchor.AddDays(7 * interval * periods),
        "Fortnightly" => anchor.AddDays(14 * interval * periods),
        "Monthly" => anchor.AddMonths(interval * periods),
        "Quarterly" => anchor.AddMonths(3 * interval * periods),
        "Annually" => anchor.AddYears(interval * periods),
        _ => anchor.AddMonths(interval * periods),
    };

    /// <summary>Amount-per-month equivalent of a schedule, for "monthly-equivalent cost" displays.</summary>
    public static decimal MonthlyEquivalent(decimal amount, string frequency, int interval)
    {
        var perOccurrence = frequency switch
        {
            "Weekly" => amount * 52m / 12m,
            "Fortnightly" => amount * 26m / 12m,
            "Monthly" => amount,
            "Quarterly" => amount / 3m,
            "Annually" => amount / 12m,
            _ => amount,
        };

        return perOccurrence / interval;
    }
}
