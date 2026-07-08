namespace MobmekApi.Services;

/// <summary>
/// Polls the email provider every 2 minutes for delivery status on recently sent emails
/// (<see cref="IOutboundEmailService.GetPendingStatusChecksAsync"/>: Status = Sent, younger than
/// 72 hours) and applies any change through the no-regress state machine. Polling is the
/// baseline delivery-status mechanism here — nothing depends on the provider's webhook, since
/// this app has no public HTTPS endpoint yet for it to call.
/// </summary>
public class OutboundStatusPollJob(IServiceScopeFactory scopeFactory, ILogger<OutboundStatusPollJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        await PollAsync(stoppingToken);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PollAsync(stoppingToken);
        }
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var outboundEmailService = scope.ServiceProvider.GetRequiredService<IOutboundEmailService>();
            var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

            var pending = await outboundEmailService.GetPendingStatusChecksAsync(cancellationToken);

            foreach (var check in pending)
            {
                var status = await emailSender.GetStatusAsync(check.ProviderMessageId, cancellationToken);
                await outboundEmailService.ApplyStatusAsync(check.Id, status.Status, status.Reason, DateTime.UtcNow, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Outbound email status poll failed");
        }
    }
}
