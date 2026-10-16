namespace MobmekApi.DTOs;

/// <summary>Connection health for the Settings page (<c>docs/xero-integration-design.md</c> §5).</summary>
public record XeroStatusDto(
    bool Configured,
    bool Connected,
    string Status,
    string? TenantName,
    DateTime? ConnectedAtUtc,
    string? LastError);

/// <summary>Returned by <c>POST .../connect</c> — the frontend does a full-page navigation to this URL.</summary>
public record XeroConnectResponseDto(string AuthorizationUrl);

/// <summary>Posted by the frontend's OAuth callback page once Xero redirects back with these two values.</summary>
public record XeroCompleteConnectionRequest(string Code, string State);
