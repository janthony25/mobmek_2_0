namespace MobmekApi.Services;

/// <summary>
/// Proactively refreshes the Xero access/refresh token pair once a day, independent of any sync
/// activity (<c>docs/xero-integration-design.md</c> §6). Xero's refresh token has a 60-day
/// sliding expiry if unused — a single-mechanic shop that goes a week or two without generating
/// an invoice must not let the connection silently die from inactivity once Phase 1 depends on it.
/// A no-op tick when unconfigured/never connected, same graceful-degradation shape as the rest
/// of the app's background jobs.
/// </summary>
public class XeroTokenRefreshJob(IServiceScopeFactory scopeFactory, ILogger<XeroTokenRefreshJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        await RefreshIfConnectedAsync(stoppingToken);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshIfConnectedAsync(stoppingToken);
        }
    }

    private async Task RefreshIfConnectedAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var xeroAuthService = scope.ServiceProvider.GetRequiredService<IXeroAuthService>();
            if (!xeroAuthService.IsConfigured)
            {
                return;
            }

            await xeroAuthService.RefreshAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Daily Xero token refresh sweep failed");
        }
    }
}
