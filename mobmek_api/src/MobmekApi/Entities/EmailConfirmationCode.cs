namespace MobmekApi.Entities;

/// <summary>
/// A one-time code emailed to a newly created account so it can confirm its email and set its
/// first password before it's allowed to sign in. Same shape/lifetime as
/// <see cref="PasswordChangeCode"/>: <see cref="ExpiresAtUtc"/> is ~10 minutes after issue, and
/// <see cref="ConsumedAtUtc"/> is set either on successful use or when a newer code supersedes it.
/// </summary>
public class EmailConfirmationCode : BaseEntity
{
    public required Guid UserId { get; set; }

    /// <summary>SHA-256 hash of the 6-digit code — never stored/logged in plain text.</summary>
    public required string CodeHash { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? ConsumedAtUtc { get; set; }
}
