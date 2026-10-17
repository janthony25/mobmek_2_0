using MobmekApi.Data;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

/// <summary>
/// Pushes due <see cref="XeroSyncItem"/> outbox rows to Xero every minute
/// (docs/xero-integration-design.md §4) — pattern: <see cref="CalendarSyncJob"/>. A no-op tick
/// when Xero isn't connected or the default sales account code hasn't been set yet (Phase 1
/// can't map invoice lines without it). Success removes the outbox row; failure records the
/// error on both the outbox row (for retry/backoff) and the invoice itself (for visibility) and
/// backs off (1 min → 5 min → 30 min → hourly), same ladder as the calendar sync.
/// </summary>
public class XeroSyncJob(IServiceScopeFactory scopeFactory, ILogger<XeroSyncJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        await PushDueItemsAsync(stoppingToken);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PushDueItemsAsync(stoppingToken);
        }
    }

    /// <summary>One sweep of due outbox rows. Public so tests can drive it directly instead of
    /// running the hosted timer loop.</summary>
    public async Task PushDueItemsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var authService = scope.ServiceProvider.GetRequiredService<IXeroAuthService>();
            var accountingClient = scope.ServiceProvider.GetRequiredService<IXeroAccountingClient>();
            var businessDetailsService = scope.ServiceProvider.GetRequiredService<IBusinessDetailsService>();

            if (!authService.IsConfigured)
            {
                return;
            }

            var accessToken = await authService.GetValidAccessTokenAsync(cancellationToken);
            var tenantId = await authService.GetTenantIdAsync(cancellationToken);
            var connection = await db.XeroConnections.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
            var defaultAccountCode = connection?.DefaultSalesAccountCode;

            if (accessToken is null || tenantId is null || string.IsNullOrWhiteSpace(defaultAccountCode))
            {
                return;
            }

            var now = DateTime.UtcNow;
            var dueItems = await db.XeroSyncItems
                .Where(i => i.NextAttemptUtc <= now)
                .OrderBy(i => i.NextAttemptUtc)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            foreach (var item in dueItems)
            {
                await ProcessItemAsync(db, accountingClient, businessDetailsService, accessToken, tenantId, defaultAccountCode, item, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Xero sync push sweep failed");
        }
    }

    private async Task ProcessItemAsync(
        AppDbContext db, IXeroAccountingClient client, IBusinessDetailsService businessDetailsService,
        string accessToken, string tenantId, string defaultAccountCode, XeroSyncItem item, CancellationToken cancellationToken)
    {
        Invoice? invoice = null;
        try
        {
            invoice = await db.Invoices
                .Include(i => i.Items)
                .Include(i => i.Job).ThenInclude(j => j!.Customer)
                .FirstOrDefaultAsync(i => i.Id == item.InvoiceId, cancellationToken);

            if (invoice is null)
            {
                // Shouldn't happen (invoices are never hard-deleted) but mirrors the calendar
                // sync's own defensive handling rather than assuming the invariant forever.
                db.XeroSyncItems.Remove(item);
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            if (item.Action == XeroSyncAction.VoidInvoice)
            {
                await ProcessVoidAsync(client, accessToken, tenantId, invoice, cancellationToken);
            }
            else
            {
                await ProcessPushAsync(client, businessDetailsService, accessToken, tenantId, defaultAccountCode, invoice, cancellationToken);
            }

            db.XeroSyncItems.Remove(item);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            item.Attempts++;
            item.LastError = ex.Message;
            item.NextAttemptUtc = DateTime.UtcNow + BackoffFor(item.Attempts);

            if (invoice is not null)
            {
                invoice.XeroSyncStatus = XeroInvoiceSyncStatus.Failed;
                invoice.XeroLastError = ex.Message;
            }

            var failingSince = DateTime.UtcNow - item.CreatedAtUtc;
            if (failingSince > TimeSpan.FromHours(24))
            {
                logger.LogError(ex, "Xero sync item {ItemId} (invoice {InvoiceId}) has been failing for {Elapsed} — likely a config problem",
                    item.Id, item.InvoiceId, failingSince);
            }
            else
            {
                logger.LogWarning(ex, "Xero sync item {ItemId} (invoice {InvoiceId}) failed, attempt {Attempts}",
                    item.Id, item.InvoiceId, item.Attempts);
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task ProcessVoidAsync(
        IXeroAccountingClient client, string accessToken, string tenantId, Invoice invoice, CancellationToken cancellationToken)
    {
        if (invoice.XeroInvoiceId is null || invoice.XeroSyncStatus == XeroInvoiceSyncStatus.Voided)
        {
            // Never pushed, or already voided (e.g. a duplicate reject) — nothing to do.
            return;
        }

        await client.VoidInvoiceAsync(accessToken, tenantId, invoice.XeroInvoiceId, cancellationToken);
        invoice.XeroSyncStatus = XeroInvoiceSyncStatus.Voided;
        invoice.XeroLastSyncedUtc = DateTime.UtcNow;
        invoice.XeroLastError = null;
    }

    private static async Task ProcessPushAsync(
        IXeroAccountingClient client, IBusinessDetailsService businessDetailsService,
        string accessToken, string tenantId, string defaultAccountCode, Invoice invoice, CancellationToken cancellationToken)
    {
        var customer = invoice.Job?.Customer
            ?? throw new InvalidOperationException("Invoice's job has no linked customer to resolve a Xero contact for.");

        var contactId = customer.XeroContactId;
        if (contactId is null)
        {
            if (!string.IsNullOrWhiteSpace(customer.EmailAddress))
            {
                contactId = await client.FindContactIdByEmailAsync(accessToken, tenantId, customer.EmailAddress, cancellationToken);
            }

            contactId ??= await client.CreateContactAsync(
                accessToken, tenantId, $"{customer.FirstName} {customer.LastName}", customer.EmailAddress, cancellationToken);
            customer.XeroContactId = contactId;
        }

        var business = await businessDetailsService.GetCurrentAsync(cancellationToken);
        var invoiceNumber = $"{business.InvoicePrefix}-{invoice.SequenceNumber:D4}";

        var request = new XeroPushInvoiceRequest(
            ExistingXeroInvoiceId: invoice.XeroInvoiceId,
            ContactId: contactId,
            InvoiceNumber: invoiceNumber,
            IssueDate: DateOnly.FromDateTime(invoice.CreatedAtUtc),
            DueDate: invoice.DueDate,
            Reference: invoice.IssueName,
            // Tax type is a fixed lookup (NZ standard 15% GST on income) — see §4.1, no
            // per-invoice tax logic exists today (GstRate is always 0.15 in practice).
            Lines: invoice.Items.Select(i => new XeroInvoiceLinePayload(i.ItemName, i.Quantity, i.ItemPrice, defaultAccountCode, "OUTPUT2")).ToList());

        var result = await client.PushInvoiceAsync(accessToken, tenantId, request, cancellationToken);

        invoice.XeroInvoiceId = result.XeroInvoiceId;
        invoice.XeroSyncStatus = XeroInvoiceSyncStatus.Synced;
        invoice.XeroLastSyncedUtc = DateTime.UtcNow;
        invoice.XeroLastError = null;
    }

    private static TimeSpan BackoffFor(int attempts) => attempts switch
    {
        1 => TimeSpan.FromMinutes(1),
        2 => TimeSpan.FromMinutes(5),
        3 => TimeSpan.FromMinutes(30),
        _ => TimeSpan.FromHours(1),
    };
}
