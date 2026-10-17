using MobmekApi.DTOs;
using MobmekApi.Services;

namespace MobmekApi.Tests.Fakes;

/// <summary>
/// Scriptable <see cref="IXeroAuthService"/> test double. Defaults to <see cref="IsConfigured"/> =
/// false, matching "Xero was never connected" — so every existing <c>InvoiceService</c> test that
/// doesn't care about Xero sync keeps behaving exactly as before.
/// </summary>
public class FakeXeroAuthService : IXeroAuthService
{
    public bool IsConfigured { get; set; }

    public string? AccessToken { get; set; } = "fake-access-token";

    public string? TenantId { get; set; } = "fake-tenant-id";

    public string? DefaultSalesAccountCode { get; set; }

    public Task<XeroStatusDto> GetStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new XeroStatusDto(
            IsConfigured, IsConfigured, IsConfigured ? "Connected" : "Disconnected", null, null, null, DefaultSalesAccountCode));

    public Task<string> BuildAuthorizationUrlAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by InvoiceService tests.");

    public Task<XeroStatusDto> CompleteConnectionAsync(string code, string state, Guid? userId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by InvoiceService tests.");

    public Task<XeroStatusDto> DisconnectAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by InvoiceService tests.");

    public Task<XeroStatusDto> RefreshAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by InvoiceService tests.");

    public Task<string?> GetValidAccessTokenAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(IsConfigured ? AccessToken : null);

    public Task<string?> GetTenantIdAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(IsConfigured ? TenantId : null);

    public Task<XeroStatusDto> SetDefaultSalesAccountCodeAsync(string? code, CancellationToken cancellationToken = default)
    {
        DefaultSalesAccountCode = string.IsNullOrWhiteSpace(code) ? null : code;
        return GetStatusAsync(cancellationToken);
    }
}
