using System.Net;
using System.Net.Http.Json;
using MobmekApi.Data;
using MobmekApi.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace MobmekApi.Tests.Services;

/// <summary>Same pattern as <c>ResendEmailSenderTests.StubHttpMessageHandler</c> — a scriptable
/// response queue instead of a real network call.</summary>
internal class StubXeroHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        await Task.Yield();
        return _responses.Count > 0 ? _responses.Dequeue() : new HttpResponseMessage(HttpStatusCode.OK);
    }
}

public class XeroAuthServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static (XeroAuthService Service, AppDbContext Db, StubXeroHttpMessageHandler Handler, FixedClock Clock) Build(
        bool configured = true, DateTimeOffset? now = null)
    {
        var db = CreateContext();
        var handler = new StubXeroHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configured
                ? new Dictionary<string, string?>
                {
                    ["Xero:ClientId"] = "client-id",
                    ["Xero:ClientSecret"] = "client-secret",
                    ["Xero:RedirectUri"] = "https://app.example/settings/xero/callback",
                }
                : [])
            .Build();
        var clock = new FixedClock(now ?? DateTimeOffset.UtcNow);
        var dataProtectionProvider = new EphemeralDataProtectionProvider();

        var service = new XeroAuthService(httpClient, db, dataProtectionProvider, config, clock, NullLogger<XeroAuthService>.Instance);
        return (service, db, handler, clock);
    }

    private static HttpResponseMessage TokenResponse(string accessToken = "access-1", string refreshToken = "refresh-1") =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                access_token = accessToken,
                refresh_token = refreshToken,
                expires_in = 1800,
                scope = "offline_access accounting.transactions",
            }),
        };

    private static HttpResponseMessage ConnectionsResponse() =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new[] { new { tenantId = "tenant-1", tenantName = "Mobmek Workshop" } }),
        };

    [Fact]
    public async Task IsConfigured_FalseWhenAnyConfigKeyMissing()
    {
        var (service, _, _, _) = Build(configured: false);
        Assert.False(service.IsConfigured);
    }

    [Fact]
    public async Task BuildAuthorizationUrlAsync_Unconfigured_Throws()
    {
        var (service, _, _, _) = Build(configured: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.BuildAuthorizationUrlAsync());
    }

    [Fact]
    public async Task BuildAuthorizationUrlAsync_StoresStateAndReturnsXeroConsentUrl()
    {
        var (service, db, _, _) = Build();

        var url = await service.BuildAuthorizationUrlAsync();

        Assert.StartsWith("https://login.xero.com/identity/connect/authorize", url);
        Assert.Contains("client_id=client-id", url);

        var connection = await db.XeroConnections.SingleAsync();
        Assert.NotNull(connection.PendingOAuthState);
        Assert.Contains($"state={connection.PendingOAuthState}", url);
    }

    [Fact]
    public async Task CompleteConnectionAsync_ValidState_StoresEncryptedTokensAndTenant()
    {
        var (service, db, handler, _) = Build();
        await service.BuildAuthorizationUrlAsync();
        var state = (await db.XeroConnections.SingleAsync()).PendingOAuthState!;

        handler.Enqueue(TokenResponse());
        handler.Enqueue(ConnectionsResponse());

        var userId = Guid.NewGuid();
        var status = await service.CompleteConnectionAsync("auth-code", state, userId);

        Assert.True(status.Connected);
        Assert.Equal("Mobmek Workshop", status.TenantName);

        var connection = await db.XeroConnections.SingleAsync();
        Assert.Equal(userId, connection.ConnectedByUserId);
        Assert.Null(connection.PendingOAuthState);
        // Stored value must not be the raw token — it's Data-Protection-encrypted.
        Assert.NotEqual("access-1", connection.AccessToken);
        Assert.NotEmpty(connection.AccessToken!);
    }

    [Fact]
    public async Task CompleteConnectionAsync_WrongState_ThrowsAndDoesNotCallXero()
    {
        var (service, db, handler, _) = Build();
        await service.BuildAuthorizationUrlAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CompleteConnectionAsync("auth-code", "not-the-real-state", null));

        Assert.Empty(handler.Requests);
        var connection = await db.XeroConnections.SingleAsync();
        Assert.Equal(Entities.XeroConnectionStatus.Disconnected, connection.Status);
    }

    [Fact]
    public async Task CompleteConnectionAsync_ExpiredState_Throws()
    {
        var (service, db, _, clock) = Build(now: DateTimeOffset.UtcNow);
        await service.BuildAuthorizationUrlAsync();
        var state = (await db.XeroConnections.SingleAsync()).PendingOAuthState!;

        clock.Advance(TimeSpan.FromMinutes(11));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteConnectionAsync("auth-code", state, null));
    }

    [Fact]
    public async Task CompleteConnectionAsync_StateIsOneTimeUse()
    {
        var (service, db, handler, _) = Build();
        await service.BuildAuthorizationUrlAsync();
        var state = (await db.XeroConnections.SingleAsync()).PendingOAuthState!;

        handler.Enqueue(TokenResponse());
        handler.Enqueue(ConnectionsResponse());
        await service.CompleteConnectionAsync("auth-code", state, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteConnectionAsync("auth-code", state, null));
    }

    [Fact]
    public async Task DisconnectAsync_ClearsTokensAndTenant()
    {
        var (service, db, handler, _) = Build();
        await service.BuildAuthorizationUrlAsync();
        var state = (await db.XeroConnections.SingleAsync()).PendingOAuthState!;
        handler.Enqueue(TokenResponse());
        handler.Enqueue(ConnectionsResponse());
        await service.CompleteConnectionAsync("auth-code", state, null);

        var status = await service.DisconnectAsync();

        Assert.False(status.Connected);
        var connection = await db.XeroConnections.SingleAsync();
        Assert.Null(connection.AccessToken);
        Assert.Null(connection.RefreshToken);
        Assert.Null(connection.TenantId);
    }

    [Fact]
    public async Task RefreshAsync_NeverConnected_NoOpsWithoutCallingXero()
    {
        var (service, _, handler, _) = Build();

        await service.RefreshAsync();

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task RefreshAsync_Success_RotatesTokensAndClearsError()
    {
        var (service, db, handler, _) = Build();
        await service.BuildAuthorizationUrlAsync();
        var state = (await db.XeroConnections.SingleAsync()).PendingOAuthState!;
        handler.Enqueue(TokenResponse());
        handler.Enqueue(ConnectionsResponse());
        await service.CompleteConnectionAsync("auth-code", state, null);

        handler.Enqueue(TokenResponse(accessToken: "access-2", refreshToken: "refresh-2"));
        var status = await service.RefreshAsync();

        Assert.True(status.Connected);
        Assert.Null(status.LastError);
    }

    [Fact]
    public async Task RefreshAsync_Failure_RecordsErrorAndSetsStatusToError()
    {
        var (service, db, handler, _) = Build();
        await service.BuildAuthorizationUrlAsync();
        var state = (await db.XeroConnections.SingleAsync()).PendingOAuthState!;
        handler.Enqueue(TokenResponse());
        handler.Enqueue(ConnectionsResponse());
        await service.CompleteConnectionAsync("auth-code", state, null);

        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = JsonContent.Create(new { error = "invalid_grant" }) });
        var status = await service.RefreshAsync();

        Assert.Equal("Error", status.Status);
        Assert.NotNull(status.LastError);
    }

    [Fact]
    public async Task GetValidAccessTokenAsync_NotConnected_ReturnsNull()
    {
        var (service, _, _, _) = Build();
        Assert.Null(await service.GetValidAccessTokenAsync());
    }

    [Fact]
    public async Task GetValidAccessTokenAsync_NearExpiry_RefreshesFirst()
    {
        var (service, db, handler, clock) = Build();
        await service.BuildAuthorizationUrlAsync();
        var state = (await db.XeroConnections.SingleAsync()).PendingOAuthState!;
        handler.Enqueue(TokenResponse(accessToken: "access-1"));
        handler.Enqueue(ConnectionsResponse());
        await service.CompleteConnectionAsync("auth-code", state, null);

        // Access token is a 1800s (30 min) token — jump to 29 minutes in, inside the 5-minute buffer.
        clock.Advance(TimeSpan.FromMinutes(29));
        handler.Enqueue(TokenResponse(accessToken: "access-2"));

        var token = await service.GetValidAccessTokenAsync();

        Assert.Equal("access-2", token);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
