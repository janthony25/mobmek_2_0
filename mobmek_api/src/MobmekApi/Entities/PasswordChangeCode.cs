namespace MobmekApi.Entities;

/// <summary>
/// A one-time code emailed to a user to authorize changing their own password without
/// requiring their current one. Short-lived and single-use: <see cref="ExpiresAtUtc"/> is
/// ~10 minutes after issue, and <see cref="ConsumedAtUtc"/> is set either when it's
/// successfully used or when a newer code supersedes it (only the latest code is ever valid).
/// </summary>
public class PasswordChangeCode : BaseEntity
{
    public required Guid UserId { get; set; }

    /// <summary>SHA-256 hash of the 6-digit code — never stored/logged in plain text.</summary>
    public required string CodeHash { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? ConsumedAtUtc { get; set; }
}
