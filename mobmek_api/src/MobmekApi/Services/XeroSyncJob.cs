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
            // Xero's validation error bodies (echoed back verbatim in the exception message —
            // see XeroAccountingClient.ReadAsync) routinely exceed the LastError columns' 2000-char
            // limit, which used to make *recording* the failure throw too — the whole recovery
            // save then rolled back (Attempts/NextAttemptUtc never advanced), leaving the item
            // permanently "due now" and silently hammering Xero every tick forever with zero
            // visibility into why. Confirmed live, 2026-10-17. Truncate before persisting.
            var errorMessage = TruncateForColumn(ex.Message);
            item.Attempts++;
            item.LastError = errorMessage;
            item.NextAttemptUtc = DateTime.UtcNow + BackoffFor(item.Attempts);

            if (invoice is not null)
            {
                invoice.XeroSyncStatus = XeroInvoiceSyncStatus.Failed;
                invoice.XeroLastError = errorMessage;
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
            // Xero requires a DueDate on an AUTHORISED invoice (confirmed live, 2026-10-17 — a
            // push with Mobmek's own nullable DueDate left unset 400'd with "The document
            // DueDate field must be specified"). Mobmek lets an invoice be generated with no due
            // date at all, so default to the issue date ("due on receipt") rather than inventing
            // a payment-term length — never written back onto Invoice.DueDate itself.
            DueDate: invoice.DueDate ?? DateOnly.FromDateTime(invoice.CreatedAtUtc),
            Reference: invoice.IssueName,
            // Tax type is a fixed lookup (NZ standard 15% GST on income) — see §4.1, no
            // per-invoice tax logic exists today (GstRate is always 0.15 in practice).
            Lines: BuildLines(invoice, defaultAccountCode));

        var result = await client.PushInvoiceAsync(accessToken, tenantId, request, cancellationToken);

        invoice.XeroInvoiceId = result.XeroInvoiceId;
        invoice.XeroSyncStatus = XeroInvoiceSyncStatus.Synced;
        invoice.XeroLastSyncedUtc = DateTime.UtcNow;
        invoice.XeroLastError = null;
    }

    /// <summary>Mirrors <c>InvoiceService.GenerateDocumentAsync</c>'s own math — subtract the
    /// discount before tax, not after. Xero has no invoice-level discount field, so (matching
    /// Xero's own documented pattern for this) it's sent as one extra negative line at the same
    /// tax type; a negative line reduces Xero's own computed GST proportionally, landing on the
    /// exact same Total as Mobmek's snapshot. Confirmed live, 2026-10-17: a discounted invoice's
    /// undiscounted raw lines were being pushed with no reduction at all, overstating the Xero
    /// total by the discount amount.</summary>
    private static List<XeroInvoiceLinePayload> BuildLines(Invoice invoice, string defaultAccountCode)
    {
        var lines = invoice.Items
            .Select(i => new XeroInvoiceLinePayload(i.ItemName, i.Quantity, i.ItemPrice, defaultAccountCode, "OUTPUT2"))
            .ToList();

        if (invoice.Discount > 0)
        {
            lines.Add(new XeroInvoiceLinePayload("Discount", 1, -invoice.Discount, defaultAccountCode, "OUTPUT2"));
        }

        return lines;
    }

    // Matches the LastError columns' HasMaxLength(2000) on both XeroSyncItem and Invoice.
    private const int MaxErrorLength = 2000;

    private static string TruncateForColumn(string message) =>
        message.Length <= MaxErrorLength ? message : string.Concat(message.AsSpan(0, MaxErrorLength - 1), "…");

    private static TimeSpan BackoffFor(int attempts) => attempts switch
    {
        1 => TimeSpan.FromMinutes(1),
        2 => TimeSpan.FromMinutes(5),
        3 => TimeSpan.FromMinutes(30),
        _ => TimeSpan.FromHours(1),
    };
}
