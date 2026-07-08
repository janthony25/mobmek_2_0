namespace MobmekApi.Entities;

/// <summary>
/// Singleton configuration row for outbound email (invoice sends etc). Exactly one row is
/// expected. Secrets (the Resend API key) live in configuration, never here — see
/// <c>Email:Resend:ApiKey</c>.
/// </summary>
public class EmailSettings : BaseEntity
{
    public string FromName { get; set; } = "Mobmek Workshop";

    public string FromAddress { get; set; } = "";

    /// <summary>Where customer replies should land — the workshop's real inbox.</summary>
    public string? ReplyToAddress { get; set; }

    /// <summary>Copy every outbound email to <see cref="ReplyToAddress"/> so it's also visible there.</summary>
    public bool BccSelf { get; set; } = true;
}
