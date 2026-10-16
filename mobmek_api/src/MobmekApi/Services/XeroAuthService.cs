using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

/// <summary>
/// Real <see cref="IXeroAuthService"/>, backed by plain HTTP calls to Xero's identity/accounting
/// hosts — hand-rolled rather than a third-party SDK dependency (no sign-off needed, full control
/// over token encryption; <c>docs/xero-integration-design.md</c> §1). Access/refresh tokens are
/// encrypted at rest via <see cref="IDataProtectionProvider"/> before ever reaching Postgres —
/// the keys backing that are already persisted to a volume in <c>docker-compose.yml</c> for the
/// auth cookie, so no new persistence setup is needed.
/// </summary>
public class XeroAuthService(
    HttpClient httpClient,
    AppDbContext db,
    IDataProtectionProvider dataProtectionProvider,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<XeroAuthService> logger) : IXeroAuthService
{
    private const string AuthorizeUrl = "https://login.xero.com/identity/connect/authorize";
    private const string TokenUrl = "https://identity.xero.com/connect/token";
    private const string ConnectionsUrl = "https://api.xero.com/connections";

    // offline_access is required to receive a refresh token at all; the other two are the
    // minimum Phase 1 (invoices/contacts) and the settings-page account-code dropdown need.
    private const string Scopes = "offline_access accounting.transactions accounting.contacts accounting.settings.read";

    private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RefreshBuffer = TimeSpan.FromMinutes(5);

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("XeroConnection");

    private string? ClientId => configuration["Xero:ClientId"];
    private string? ClientSecret => configuration["Xero:ClientSecret"];
    private string? RedirectUri => configuration["Xero:RedirectUri"];

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret) && !string.IsNullOrWhiteSpace(RedirectUri);

    public async Task<XeroStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetOrCreateConnectionAsync(cancellationToken);
        return ToDto(connection);
    }

    public async Task<string> BuildAuthorizationUrlAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Xero:ClientId/ClientSecret/RedirectUri are not configured — set them before connecting.");
        }

        var connection = await GetOrCreateConnectionAsync(cancellationToken);
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        connection.PendingOAuthState = state;
        connection.PendingOAuthStateExpiresUtc = timeProvider.GetUtcNow().UtcDateTime + StateTtl;
        await db.SaveChangesAsync(cancellationToken);

        return QueryHelpers.AddQueryString(AuthorizeUrl, new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = ClientId,
            ["redirect_uri"] = RedirectUri,
            ["scope"] = Scopes,
            ["state"] = state,
        });
    }

    public async Task<XeroStatusDto> CompleteConnectionAsync(
        string code, string state, Guid? userId, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Xero:ClientId/ClientSecret/RedirectUri are not configured.");
        }

        var connection = await GetOrCreateConnectionAsync(cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (connection.PendingOAuthState is null
            || connection.PendingOAuthStateExpiresUtc is null
            || connection.PendingOAuthStateExpiresUtc < now
            || !string.Equals(connection.PendingOAuthState, state, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "This Xero connection attempt expired or doesn't match — click \"Connect to Xero\" again.");
        }

        // One-time use regardless of outcome below — a replayed callback must not succeed twice.
        connection.PendingOAuthState = null;
        connection.PendingOAuthStateExpiresUtc = null;
        await db.SaveChangesAsync(cancellationToken);

        var tokenResponse = await ExchangeAsync(
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = RedirectUri!,
            },
            cancellationToken);

        ApplyTokenResponse(connection, tokenResponse, now);

        var tenant = await FetchTenantAsync(tokenResponse.AccessToken, cancellationToken);
        connection.TenantId = tenant?.TenantId;
        connection.TenantName = tenant?.TenantName;
        connection.Status = XeroConnectionStatus.Connected;
        connection.ConnectedAtUtc = now;
        connection.ConnectedByUserId = userId;
        connection.LastError = null;

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Xero connected to tenant {TenantName} ({TenantId}).", connection.TenantName, connection.TenantId);
        return ToDto(connection);
    }

    public async Task<XeroStatusDto> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetOrCreateConnectionAsync(cancellationToken);
        connection.Status = XeroConnectionStatus.Disconnected;
        connection.AccessToken = null;
        connection.RefreshToken = null;
        connection.AccessTokenExpiresUtc = null;
        connection.TenantId = null;
        connection.TenantName = null;
        connection.Scopes = null;
        connection.LastError = null;
        connection.ConnectedAtUtc = null;
        connection.ConnectedByUserId = null;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(connection);
    }

    public async Task<XeroStatusDto> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetOrCreateConnectionAsync(cancellationToken);
        if (connection.RefreshToken is null)
        {
            // Nothing to refresh — either never connected, or already disconnected.
            return ToDto(connection);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        try
        {
            var refreshToken = _protector.Unprotect(connection.RefreshToken);
            var tokenResponse = await ExchangeAsync(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                },
                cancellationToken);

            ApplyTokenResponse(connection, tokenResponse, now);
            connection.Status = XeroConnectionStatus.Connected;
            connection.LastError = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            connection.Status = XeroConnectionStatus.Error;
            connection.LastError = ex.Message;
            logger.LogError(ex, "Xero token refresh failed — reconnect required.");
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(connection);
    }

    public async Task<string?> GetValidAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetOrCreateConnectionAsync(cancellationToken);
        if (connection.Status != XeroConnectionStatus.Connected || connection.AccessToken is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (connection.AccessTokenExpiresUtc is null || connection.AccessTokenExpiresUtc.Value - now < RefreshBuffer)
        {
            await RefreshAsync(cancellationToken);
            connection = await GetOrCreateConnectionAsync(cancellationToken);
            if (connection.Status != XeroConnectionStatus.Connected || connection.AccessToken is null)
            {
                return null;
            }
        }

        return _protector.Unprotect(connection.AccessToken);
    }

    private async Task<XeroConnection> GetOrCreateConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = await db.XeroConnections.FirstOrDefaultAsync(cancellationToken);
        if (connection is not null)
        {
            return connection;
        }

        connection = new XeroConnection();
        db.XeroConnections.Add(connection);
        await db.SaveChangesAsync(cancellationToken);
        return connection;
    }

    private void ApplyTokenResponse(XeroConnection connection, XeroTokenResponse tokenResponse, DateTime now)
    {
        connection.AccessToken = _protector.Protect(tokenResponse.AccessToken);
        connection.RefreshToken = _protector.Protect(tokenResponse.RefreshToken);
        connection.AccessTokenExpiresUtc = now + TimeSpan.FromSeconds(tokenResponse.ExpiresIn);
        connection.Scopes = tokenResponse.Scope;
    }

    private async Task<XeroTokenResponse> ExchangeAsync(Dictionary<string, string> formFields, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl) { Content = new FormUrlEncodedContent(formFields) };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}")));

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Xero token endpoint returned {(int)response.StatusCode}: {body}");
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<XeroTokenResponse>(cancellationToken: cancellationToken);
        return tokenResponse ?? throw new InvalidOperationException("Xero token endpoint returned an empty body.");
    }

    /// <summary>Single-organisation connection (design doc §1) — just takes the first entry.</summary>
    private async Task<XeroTenant?> FetchTenantAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ConnectionsUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var tenants = await response.Content.ReadFromJsonAsync<List<XeroTenant>>(cancellationToken: cancellationToken);
        return tenants?.FirstOrDefault();
    }

    private XeroStatusDto ToDto(XeroConnection connection) => new(
        IsConfigured,
        connection.Status == XeroConnectionStatus.Connected,
        connection.Status.ToString(),
        connection.TenantName,
        connection.ConnectedAtUtc,
        connection.LastError);

    private record XeroTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("scope")] string? Scope);

    private record XeroTenant(
        [property: JsonPropertyName("tenantId")] string TenantId,
        [property: JsonPropertyName("tenantName")] string? TenantName);
}
