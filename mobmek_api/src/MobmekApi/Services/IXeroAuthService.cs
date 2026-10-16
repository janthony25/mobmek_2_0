using MobmekApi.DTOs;

namespace MobmekApi.Services;

/// <summary>
/// Owns the single Xero OAuth2 connection (<c>docs/xero-integration-design.md</c> §3.1/§6):
/// building the consent URL, exchanging the callback code for tokens, proactive refresh, and
/// decrypting a usable access token for later Phase-1/2 API calls. Behind an interface so
/// <see cref="XeroIntegrationController"/> and <see cref="XeroTokenRefreshJob"/> can be tested
/// against a fake, and so Phase 1's push job depends on this rather than reimplementing auth.
/// </summary>
public interface IXeroAuthService
{
    /// <summary>True once <c>Xero:ClientId</c>/<c>:ClientSecret</c>/<c>:RedirectUri</c> are all
    /// present — the "unconfigured = cleanly disabled" gate, same shape as <see cref="IGoogleCalendarClient.IsConfigured"/>.</summary>
    bool IsConfigured { get; }

    Task<XeroStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Generates and stores a one-time state nonce, returns the full Xero consent URL
    /// to redirect the browser to. Throws <see cref="InvalidOperationException"/> if unconfigured.</summary>
    Task<string> BuildAuthorizationUrlAsync(CancellationToken cancellationToken = default);

    /// <summary>Validates <paramref name="state"/> against the pending nonce (and its TTL),
    /// exchanges <paramref name="code"/> for tokens, fetches the connected tenant, and stores
    /// everything. Throws <see cref="InvalidOperationException"/> on an invalid/expired state or
    /// a failed exchange — callers map that to a 400.</summary>
    Task<XeroStatusDto> CompleteConnectionAsync(string code, string state, Guid? userId, CancellationToken cancellationToken = default);

    /// <summary>Clears the stored tokens/tenant and marks the connection Disconnected. Does not
    /// call Xero to revoke consent — the owner can do that from Xero's own "connected apps" page
    /// if they want to fully revoke, same as any other OAuth app disconnect.</summary>
    Task<XeroStatusDto> DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Refreshes the access/refresh token pair now, regardless of how close the current
    /// access token is to expiring — this is what keeps the 60-day refresh-token window alive on
    /// a schedule (<see cref="XeroTokenRefreshJob"/>) rather than only when an invoice happens to
    /// sync. Swallows failures onto <see cref="Entities.XeroConnection.LastError"/> rather than
    /// throwing, since the hosted job calls this unattended.</summary>
    Task<XeroStatusDto> RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a decrypted, currently-valid access token — refreshing first if it's
    /// within 5 minutes of expiry — or null if not connected/configured. Reserved for Phase 1's
    /// push job; nothing in Phase 0 calls this yet.</summary>
    Task<string?> GetValidAccessTokenAsync(CancellationToken cancellationToken = default);
}
