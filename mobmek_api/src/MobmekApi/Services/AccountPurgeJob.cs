namespace MobmekApi.Services;

/// <summary>
/// Hourly background sweep that hard-deletes accounts an Admin deactivated 30+ days ago (see
/// <see cref="AccountAdminService.DeactivateAsync"/>). Follows the same hosted-service shape as
/// <see cref="RecurringTransactionPostingJob"/>.
/// </summary>
public class AccountPurgeJob(IServiceScopeFactory scopeFactory, ILogger<AccountPurgeJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        // Run once on startup, then on every tick thereafter.
        await PurgeExpiredAccountsAsync(stoppingToken);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PurgeExpiredAccountsAsync(stoppingToken);
        }
    }

    private async Task PurgeExpiredAccountsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var accountAdminService = scope.ServiceProvider.GetRequiredService<IAccountAdminService>();

            var purged = await accountAdminService.PurgeExpiredDeactivatedAccountsAsync(cancellationToken);
            if (purged > 0)
            {
                logger.LogInformation("Purged {Count} account(s) deactivated 30+ days ago", purged);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Deactivated-account purge sweep failed");
        }
    }
}
