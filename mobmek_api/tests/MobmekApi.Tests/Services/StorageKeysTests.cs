using MobmekApi.Services;

namespace MobmekApi.Tests.Services;

/// <summary>
/// StorageKeys is the single place both IFileStorage backends derive object keys, so these
/// tests pin down two things: the "yyyy/MM/{guid}{ext}" shape (keys already in the database
/// stay readable after a move between local disk and S3), and the rule that an uploaded
/// filename can never steer where the file lands.
/// </summary>
public class StorageKeysTests
{
    [Fact]
    public void Create_PrefixesKeyWithYearAndMonth()
    {
        var key = StorageKeys.Create("photo.jpg", new DateTime(2026, 3, 9, 12, 0, 0, DateTimeKind.Utc));

        Assert.StartsWith("2026/03/", key);
    }

    [Fact]
    public void Create_KeepsTheOriginalExtension()
    {
        var key = StorageKeys.Create("receipt.PDF", new DateTime(2026, 3, 9, 12, 0, 0, DateTimeKind.Utc));

        Assert.EndsWith(".PDF", key);
    }

    [Fact]
    public void Create_DiscardsTheOriginalFileNameKeepingOnlyTheExtension()
    {
        var key = StorageKeys.Create("invoice-for-acme.jpg", new DateTime(2026, 3, 9, 12, 0, 0, DateTimeKind.Utc));

        Assert.DoesNotContain("invoice-for-acme", key);
        // 32-char "N"-format guid between the prefix and the extension — nothing caller-supplied.
        Assert.Equal(32, key["2026/03/".Length..^".jpg".Length].Length);
    }

    [Theory]
    // A traversal attempt lands entirely in the extension, which is then rejected for both
    // reasons being tested here — it holds a path separator and it is over-long.
    [InlineData("../../../etc/passwd")]
    [InlineData("photo.jpg/../../escape")]
    [InlineData("name.thisextensioniswaytoolong")]
    public void Create_DropsExtensionsThatAreUnsafeOrOverlyLong(string fileName)
    {
        var key = StorageKeys.Create(fileName, new DateTime(2026, 3, 9, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal("2026/03/".Length + 32, key.Length);
        Assert.Equal("2026/03/", key[..8]);
    }

    [Fact]
    public void Create_NeverEscapesItsYearMonthPrefix()
    {
        var key = StorageKeys.Create("../../../etc/passwd", new DateTime(2026, 3, 9, 12, 0, 0, DateTimeKind.Utc));

        Assert.DoesNotContain("..", key);
        Assert.DoesNotContain("passwd", key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-extension")]
    public void Create_HandlesMissingNamesAndExtensions(string? fileName)
    {
        var key = StorageKeys.Create(fileName, new DateTime(2026, 3, 9, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal("2026/03/".Length + 32, key.Length);
    }

    [Fact]
    public void Create_ReturnsADistinctKeyEveryCall()
    {
        var at = new DateTime(2026, 3, 9, 12, 0, 0, DateTimeKind.Utc);

        var keys = Enumerable.Range(0, 100).Select(_ => StorageKeys.Create("photo.jpg", at)).ToList();

        // Two files uploaded in the same month must not collide and overwrite each other.
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void Create_DefaultsToTheCurrentUtcMonth()
    {
        var key = StorageKeys.Create("photo.jpg");

        Assert.StartsWith($"{DateTime.UtcNow:yyyy/MM}/", key);
    }
}
