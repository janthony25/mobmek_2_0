using MobmekApi.Entities;
using MobmekApi.LegacyImport.Legacy;

namespace MobmekApi.LegacyImport.Mappers;

public sealed record CustomerMapResult(Customer Customer, bool SingleWordName, bool MissingPhone);

/// <summary>
/// Legacy Customer → new Customer (design §3.1). The old single CustomerName splits into
/// FirstName/LastName; required-but-missing fields get flagged placeholders so the new
/// schema's invariants hold without weakening them.
/// </summary>
public static class CustomerMapper
{
    public const string PlaceholderPhone = "N/A";

    public const string PlaceholderLastName = "-";

    public static CustomerMapResult Map(LegacyCustomer legacy)
    {
        var (firstName, lastName, singleWord) = SplitName(legacy.CustomerName);
        var phone = NullIfBlank(legacy.CustomerNumber);

        var customer = new Customer
        {
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phone ?? PlaceholderPhone,
            EmailAddress = NullIfBlank(legacy.CustomerEmail),
            PhysicalAddress = NullIfBlank(legacy.CustomerAddress),
            Notes = $"Imported from legacy system (Customer #{legacy.CustomerId})",
            CreatedAtUtc = NzTime.ToUtc(legacy.DateAdded),
            UpdatedAtUtc = NzTime.ToUtc(legacy.DateEdited),
        };

        return new CustomerMapResult(customer, singleWord, phone is null);
    }

    /// <summary>First word → FirstName, remainder → LastName; single-word names get LastName "-".</summary>
    public static (string FirstName, string LastName, bool SingleWord) SplitName(string customerName)
    {
        var parts = customerName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => (PlaceholderLastName, PlaceholderLastName, true),
            1 => (parts[0], PlaceholderLastName, true),
            _ => (parts[0], string.Join(' ', parts[1..]), false),
        };
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
