namespace MobmekApi.Entities;

/// <summary>
/// One login attempt, successful or not — the trail an Admin checks for suspicious activity
/// (repeated failures, logins at odd hours). CreatedAtUtc (from BaseEntity) is the attempt time.
/// </summary>
public class LoginAttempt : BaseEntity
{
    public required string Email { get; set; }

    /// <summary>Set only when the email matched a real account, whether or not the password did.</summary>
    public Guid? EmployeeId { get; set; }

    public Employee? Employee { get; set; }

    public bool Succeeded { get; set; }

    /// <summary>e.g. "InvalidCredentials", "LockedOut" — null on success.</summary>
    public string? FailureReason { get; set; }

    /// <summary>Best-effort: behind the nginx reverse proxy this reflects the proxy hop, not the
    /// real client, until forwarded-header trust is configured for the target deployment.</summary>
    public string? IpAddress { get; set; }
}
