namespace MobmekApi.Services;

/// <summary>
/// Hourly background sweep that auto-posts due <see cref="Entities.RecurringTransaction"/>
/// occurrences whose <see cref="Entities.RecurringTransaction.AutoPost"/> is true. Schedules
/// with <c>AutoPost = false</c> (the default) are left for the "due — confirm" UI instead.
/// First consumer of the hosted-service pattern; later phases (tax obligation regeneration,
/// AI insight nightly run) can follow the same shape.
/// </summary>
public class RecurringTransactionPostingJob(IServiceScopeFactory scopeFactory, ILogger<RecurringTransactionPostingJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        // Run once on startup, then on every tick thereafter.
        await PostDueOccurrencesAsync(stoppingToken);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PostDueOccurrencesAsync(stoppingToken);
        }
    }

    private async Task PostDueOccurrencesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var recurringService = scope.ServiceProvider.GetRequiredService<IRecurringTransactionService>();

            var due = await recurringService.GetDueOccurrencesAsync(
                DateOnly.FromDateTime(DateTime.UtcNow), autoPostOnly: true, cancellationToken);

            foreach (var occurrence in due)
            {
                var (_, error) = await recurringService.PostOccurrenceAsync(
                    occurrence.RecurringTransactionId, occurrence.Date, cancellationToken);
                if (error != DTOs.RecurringTransactionWriteError.None)
                {
                    logger.LogWarning(
                        "Failed to auto-post recurring occurrence {RecurringTransactionId} on {Date}: {Error}",
                        occurrence.RecurringTransactionId, occurrence.Date, error);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Recurring transaction auto-posting sweep failed");
        }
    }
}
