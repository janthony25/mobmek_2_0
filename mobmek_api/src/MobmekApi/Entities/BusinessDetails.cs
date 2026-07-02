namespace MobmekApi.Entities;

/// <summary>
/// Singleton row holding the workshop's letterhead details, shown on generated invoices.
/// Exactly one row is expected; created on first use with a default name.
/// </summary>
public class BusinessDetails : BaseEntity
{
    public string Name { get; set; } = "Mobmek Workshop";
    public string? Address { get; set; }
    public string? Email { get; set; }
    public string? BusinessPhone { get; set; }
    public string? Telephone { get; set; }

    /// <summary>GST registration number, shown on generated invoices.</summary>
    public string? GstNumber { get; set; }

    public string? Website { get; set; }

    /// <summary>Free-text bank/payment details (account name, bank, account number) shown on invoices for bank-transfer payers.</summary>
    public string? BankDetails { get; set; }

    /// <summary>URL to a hosted logo image, rendered on the invoice letterhead.</summary>
    public string? LogoUrl { get; set; }
}
