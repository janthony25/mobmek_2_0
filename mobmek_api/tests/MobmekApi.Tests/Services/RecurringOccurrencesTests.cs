using MobmekApi.Data;

namespace MobmekApi.Tests.Services;

public class RecurringOccurrencesTests
{
    [Fact]
    public void Expand_Weekly_StepsBySevenDaysTimesInterval()
    {
        var anchor = new DateOnly(2026, 1, 5); // Monday

        var occurrences = RecurringOccurrences.Expand("Weekly", 2, anchor, null, anchor, anchor.AddDays(30)).ToList();

        Assert.Equal(
            [new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 19), new DateOnly(2026, 2, 2)],
            occurrences);
    }

    [Fact]
    public void Expand_Monthly_ClampsAtMonthEndInsteadOfOverflowing()
    {
        var anchor = new DateOnly(2026, 1, 31);

        var occurrences = RecurringOccurrences.Expand("Monthly", 1, anchor, null, anchor, new DateOnly(2026, 4, 30)).ToList();

        // Jan 31 -> Feb 28 (2026 is not a leap year) -> Mar 31 -> Apr 30, never overflowing into the next month.
        Assert.Equal(
            [new DateOnly(2026, 1, 31), new DateOnly(2026, 2, 28), new DateOnly(2026, 3, 31), new DateOnly(2026, 4, 30)],
            occurrences);
    }

    [Fact]
    public void Expand_Quarterly_StepsByThreeMonths()
    {
        var anchor = new DateOnly(2026, 1, 15);

        var occurrences = RecurringOccurrences.Expand("Quarterly", 1, anchor, null, anchor, new DateOnly(2026, 12, 31)).ToList();

        Assert.Equal(
            [new DateOnly(2026, 1, 15), new DateOnly(2026, 4, 15), new DateOnly(2026, 7, 15), new DateOnly(2026, 10, 15)],
            occurrences);
    }

    [Fact]
    public void Expand_StopsAtEndDate()
    {
        var anchor = new DateOnly(2026, 1, 1);

        var occurrences = RecurringOccurrences
            .Expand("Monthly", 1, anchor, endDate: new DateOnly(2026, 3, 15), anchor, anchor.AddYears(1))
            .ToList();

        Assert.Equal([new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 1)], occurrences);
    }

    [Fact]
    public void Expand_ExcludesOccurrencesBeforeFrom()
    {
        var anchor = new DateOnly(2026, 1, 1);

        var occurrences = RecurringOccurrences.Expand("Monthly", 1, anchor, null, new DateOnly(2026, 3, 1), new DateOnly(2026, 5, 1)).ToList();

        Assert.Equal([new DateOnly(2026, 3, 1), new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 1)], occurrences);
    }

    [Theory]
    [InlineData("Weekly", 1, 100, 433.33)]
    [InlineData("Fortnightly", 1, 100, 216.67)]
    [InlineData("Monthly", 1, 100, 100)]
    [InlineData("Quarterly", 1, 300, 100)]
    [InlineData("Annually", 1, 1200, 100)]
    [InlineData("Monthly", 2, 100, 50)]
    public void MonthlyEquivalent_ComputesPerMonthCost(string frequency, int interval, decimal amount, decimal expected)
    {
        var result = RecurringOccurrences.MonthlyEquivalent(amount, frequency, interval);

        Assert.Equal(expected, Math.Round(result, 2));
    }
}
