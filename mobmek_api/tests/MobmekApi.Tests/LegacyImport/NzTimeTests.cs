using MobmekApi.LegacyImport;

namespace MobmekApi.Tests.LegacyImport;

public class NzTimeTests
{
    [Fact]
    public void ToUtc_WinterTime_IsUtcPlus12()
    {
        // NZ standard time (June): UTC+12.
        var utc = NzTime.ToUtc(new DateTime(2026, 6, 15, 12, 0, 0));

        Assert.Equal(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc), utc);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
    }

    [Fact]
    public void ToUtc_SummerTime_IsUtcPlus13()
    {
        // NZ daylight time (January): UTC+13.
        var utc = NzTime.ToUtc(new DateTime(2026, 1, 15, 12, 0, 0));

        Assert.Equal(new DateTime(2026, 1, 14, 23, 0, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void ToUtc_SpringForwardGap_ShiftsForwardInsteadOfThrowing()
    {
        // 2026-09-27 02:30 never existed (clocks jump 02:00 → 03:00). Treated as 03:30 NZDT.
        var utc = NzTime.ToUtc(new DateTime(2026, 9, 27, 2, 30, 0));

        Assert.Equal(new DateTime(2026, 9, 26, 14, 30, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void ToUtc_FallBackAmbiguousTime_ResolvesToStandardTime()
    {
        // 2026-04-05 02:30 occurred twice (clocks fall 03:00 → 02:00); resolves to NZST (+12).
        var utc = NzTime.ToUtc(new DateTime(2026, 4, 5, 2, 30, 0));

        Assert.Equal(new DateTime(2026, 4, 4, 14, 30, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void ToUtc_Nullable_PassesThroughNull()
    {
        Assert.Null(NzTime.ToUtc(null));
        Assert.NotNull(NzTime.ToUtc((DateTime?)new DateTime(2026, 6, 15, 12, 0, 0)));
    }

    [Fact]
    public void ToUtc_DatePlusTimeOfDay_CombinesBeforeConverting()
    {
        // Legacy appointments store date and time in separate columns.
        var utc = NzTime.ToUtc(new DateTime(2026, 6, 15), new TimeSpan(9, 30, 0));

        Assert.Equal(new DateTime(2026, 6, 14, 21, 30, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void ToDateOnly_TakesTheAucklandCalendarDate()
    {
        // The wall-clock date is kept as-is — no timezone shifting for date-only fields.
        Assert.Equal(new DateOnly(2026, 1, 15), NzTime.ToDateOnly(new DateTime(2026, 1, 15, 23, 30, 0)));
        Assert.Null(NzTime.ToDateOnly(null));
    }
}
