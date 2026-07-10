using MobmekApi.Services;

namespace MobmekApi.Tests.Services;

public class UtcKindTests
{
    [Fact]
    public void Unspecified_BecomesUtcKind_SameWallClockValue()
    {
        var bare = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        var normalized = UtcKind.Normalize(bare);

        Assert.Equal(DateTimeKind.Utc, normalized.Kind);
        Assert.Equal(bare.Ticks, normalized.Ticks);
    }

    [Fact]
    public void Utc_PassesThroughUnchanged()
    {
        var utc = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(utc, UtcKind.Normalize(utc));
    }

    [Fact]
    public void Local_IsConvertedToUniversalTime()
    {
        var local = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Local);

        var normalized = UtcKind.Normalize(local);

        Assert.Equal(DateTimeKind.Utc, normalized.Kind);
        Assert.Equal(local.ToUniversalTime(), normalized);
    }

    [Fact]
    public void Null_StaysNull()
    {
        Assert.Null(UtcKind.Normalize((DateTime?)null));
    }
}
