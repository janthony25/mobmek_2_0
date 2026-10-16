namespace MobmekApi.Entities;

/// <summary>
/// Singleton row holding the live OAuth2 connection to a single Xero organisation
/// (<c>docs/xero-integration-design.md</c> §3.1/§6). Unlike the Google Calendar sync's config-only
/// secret (a static service-account key), Xero's refresh token is live state Xero rotates on every
/// use — it has to be persisted here, not in config, and <see cref="AccessToken"/>/
/// <see cref="RefreshToken"/> are stored already-encrypted (via <c>IDataProtectionProvider</c> in
/// <see cref="Services.XeroAuthService"/>) so a raw DB read never exposes a usable credential.
/// </summary>
public class XeroConnection : BaseEntity
{
    public XeroConnectionStatus Status { get; set; } = XeroConnectionStatus.Disconnected;

    /// <summary>Xero organisation id (<c>xero-tenant-id</c>), returned by the Connections endpoint
    /// at connect time. Null until a connection has ever succeeded.</summary>
    public string? TenantId { get; set; }

    /// <summary>Display name of the connected org, shown on the settings page.</summary>
    public string? TenantName { get; set; }

    /// <summary>Data-Protection-encrypted access token. Short-lived (~30 min) — always refreshed
    /// via <see cref="RefreshToken"/> rather than relied on past <see cref="AccessTokenExpiresUtc"/>.</summary>
    public string? AccessToken { get; set; }

    public DateTime? AccessTokenExpiresUtc { get; set; }

    /// <summary>Data-Protection-encrypted refresh token — the durable credential. Xero rotates it
    /// on every use and it has a 60-day sliding expiry if unused, which is why
    /// <see cref="Services.XeroTokenRefreshJob"/> refreshes proactively rather than only on demand.</summary>
    public string? RefreshToken { get; set; }

    /// <summary>Space-separated scopes actually granted, as returned by Xero.</summary>
    public string? Scopes { get; set; }

    public DateTime? ConnectedAtUtc { get; set; }

    public Guid? ConnectedByUserId { get; set; }

    /// <summary>Most recent failure (refresh failure, revoked consent, API error) — surfaced on
    /// the settings page. Cleared on the next successful refresh/connect.</summary>
    public string? LastError { get; set; }

    /// <summary>CSRF-protection nonce for the in-flight OAuth handshake, set by
    /// <c>BuildAuthorizationUrlAsync</c> and checked (and cleared) by
    /// <c>CompleteConnectionAsync</c>. Not a secret in the token sense — just proves the callback
    /// corresponds to an authorization URL this app actually issued.</summary>
    public string? PendingOAuthState { get; set; }

    /// <summary>Short TTL (10 minutes) on <see cref="PendingOAuthState"/> — an abandoned "Connect"
    /// click must not leave a state value that's valid forever.</summary>
    public DateTime? PendingOAuthStateExpiresUtc { get; set; }
}
