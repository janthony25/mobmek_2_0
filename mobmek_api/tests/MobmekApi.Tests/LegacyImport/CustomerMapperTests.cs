using MobmekApi.LegacyImport.Legacy;
using MobmekApi.LegacyImport.Mappers;

namespace MobmekApi.Tests.LegacyImport;

public class CustomerMapperTests
{
    private static LegacyCustomer Legacy(
        string name = "Marlon Mendoza",
        string? number = "0211234567",
        string? email = null,
        string? address = null) =>
        new(42, name, address, email, number, new DateTime(2024, 6, 15, 10, 0, 0), null);

    [Fact]
    public void Map_MultiWordName_SplitsFirstWordFromRest()
    {
        var (customer, singleWord, _) = CustomerMapper.Map(Legacy(name: "Juan De La Cruz"));

        Assert.Equal("Juan", customer.FirstName);
        Assert.Equal("De La Cruz", customer.LastName);
        Assert.False(singleWord);
    }

    [Fact]
    public void Map_SingleWordName_GetsPlaceholderLastName_AndIsFlagged()
    {
        var (customer, singleWord, _) = CustomerMapper.Map(Legacy(name: "HARWOOD"));

        Assert.Equal("HARWOOD", customer.FirstName);
        Assert.Equal("-", customer.LastName);
        Assert.True(singleWord);
    }

    [Fact]
    public void Map_NameWithExtraWhitespace_IsTrimmedAndCollapsed()
    {
        var (customer, singleWord, _) = CustomerMapper.Map(Legacy(name: "  Ana   Reyes  "));

        Assert.Equal("Ana", customer.FirstName);
        Assert.Equal("Reyes", customer.LastName);
        Assert.False(singleWord);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Map_MissingPhone_GetsPlaceholder_AndIsFlagged(string? phone)
    {
        var (customer, _, missingPhone) = CustomerMapper.Map(Legacy(number: phone));

        Assert.Equal("N/A", customer.PhoneNumber);
        Assert.True(missingPhone);
    }

    [Fact]
    public void Map_PresentPhone_IsKeptTrimmed_AndNotFlagged()
    {
        var (customer, _, missingPhone) = CustomerMapper.Map(Legacy(number: " 021 555 123 "));

        Assert.Equal("021 555 123", customer.PhoneNumber);
        Assert.False(missingPhone);
    }

    [Fact]
    public void Map_BlankEmailAndAddress_BecomeNull()
    {
        var (customer, _, _) = CustomerMapper.Map(Legacy(email: "  ", address: ""));

        Assert.Null(customer.EmailAddress);
        Assert.Null(customer.PhysicalAddress);
    }

    [Fact]
    public void Map_WritesProvenanceNote_AndConvertsAuditDatesToUtc()
    {
        var (customer, _, _) = CustomerMapper.Map(Legacy());

        Assert.Equal("Imported from legacy system (Customer #42)", customer.Notes);
        // 2024-06-15 10:00 NZST (+12) → 2024-06-14 22:00 UTC.
        Assert.Equal(new DateTime(2024, 6, 14, 22, 0, 0, DateTimeKind.Utc), customer.CreatedAtUtc);
        Assert.Null(customer.UpdatedAtUtc);
    }
}
