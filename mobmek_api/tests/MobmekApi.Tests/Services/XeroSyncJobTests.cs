using MobmekApi.Data;
using MobmekApi.Entities;
using MobmekApi.Services;
using MobmekApi.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MobmekApi.Tests.Services;

/// <summary>
/// Covers <see cref="XeroSyncJob"/>'s push/void logic per docs/xero-integration-design.md §4:
/// contact resolution (reuse vs find-by-email vs create), invoice mapping, void, failure backoff,
/// and the unconfigured/no-account-code no-op gates. Drives
/// <see cref="XeroSyncJob.PushDueItemsAsync"/> directly rather than running the hosted timer loop.
/// </summary>
public class XeroSyncJobTests
{
    private static (AppDbContext Db, XeroSyncJob Job, FakeXeroAccountingClient Client) CreateJob(
        bool xeroConfigured = true, string? defaultAccountCode = "200")
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        db.XeroConnections.Add(new XeroConnection
        {
            Status = xeroConfigured ? XeroConnectionStatus.Connected : XeroConnectionStatus.Disconnected,
            DefaultSalesAccountCode = defaultAccountCode,
        });
        db.SaveChanges();

        var client = new FakeXeroAccountingClient();
        var storage = new LocalFileStorage(Path.Combine(Path.GetTempPath(), "mobmek-tests", Guid.NewGuid().ToString("N")));

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<IXeroAuthService>(new FakeXeroAuthService { IsConfigured = xeroConfigured });
        services.AddSingleton<IXeroAccountingClient>(client);
        services.AddSingleton<IBusinessDetailsService>(new BusinessDetailsService(db, storage));
        services.AddLogging();
        var provider = services.BuildServiceProvider();

        var job = new XeroSyncJob(provider.GetRequiredService<IServiceScopeFactory>(), provider.GetRequiredService<ILogger<XeroSyncJob>>());
        return (db, job, client);
    }

    private static (Customer Customer, Invoice Invoice) SeedInvoice(
        AppDbContext db, string? customerEmail = "jane@example.com", string? xeroInvoiceId = null, string? xeroContactId = null)
    {
        var customer = new Customer { FirstName = "Jane", LastName = "Doe", PhoneNumber = "021", EmailAddress = customerEmail, XeroContactId = xeroContactId };
        db.Customers.Add(customer);
        var job = new Job { CustomerId = customer.Id, CarId = Guid.NewGuid(), Title = "Brakes" };
        db.Jobs.Add(job);
        var invoice = new Invoice
        {
            JobId = job.Id, IssueName = "Brakes", DocumentType = "Invoice", Status = "Active",
            SequenceNumber = 1, TotalAmount = 110m, XeroInvoiceId = xeroInvoiceId,
        };
        invoice.Items.Add(new InvoiceItem { ItemName = "Pads", Quantity = 1, ItemPrice = 110m, ItemTotal = 110m });
        db.Invoices.Add(invoice);
        job.CustomerId = customer.Id;
        db.SaveChanges();
        return (customer, invoice);
    }

    private static XeroSyncItem SeedOutboxItem(AppDbContext db, XeroSyncAction action, Guid invoiceId)
    {
        var item = new XeroSyncItem { Action = action, InvoiceId = invoiceId };
        db.XeroSyncItems.Add(item);
        db.SaveChanges();
        return item;
    }

    [Fact]
    public async Task PushInvoice_NoExistingContact_CreatesOneAndStoresIdOnCustomer()
    {
        var (db, job, client) = CreateJob();
        var (customer, invoice) = SeedInvoice(db);
        SeedOutboxItem(db, XeroSyncAction.PushInvoice, invoice.Id);

        await job.PushDueItemsAsync(CancellationToken.None);

        Assert.Single(client.CreatedContacts);
        Assert.Equal(("Jane Doe", "jane@example.com"), client.CreatedContacts[0]);
        var updatedCustomer = await db.Customers.FirstAsync(c => c.Id == customer.Id);
        Assert.NotNull(updatedCustomer.XeroContactId);

        Assert.Single(client.PushedInvoices);
        var pushed = client.PushedInvoices[0];
        Assert.Null(pushed.ExistingXeroInvoiceId);
        Assert.Equal("INV-0001", pushed.InvoiceNumber);
        Assert.Single(pushed.Lines);
        Assert.Equal("200", pushed.Lines[0].AccountCode);
        Assert.Equal("OUTPUT2", pushed.Lines[0].TaxType);

        var updatedInvoice = await db.Invoices.FirstAsync(i => i.Id == invoice.Id);
        Assert.NotNull(updatedInvoice.XeroInvoiceId);
        Assert.Equal(XeroInvoiceSyncStatus.Synced, updatedInvoice.XeroSyncStatus);
        Assert.Empty(await db.XeroSyncItems.ToListAsync());
    }

    [Fact]
    public async Task PushInvoice_ExistingXeroContactId_ReusesIt_NoFindOrCreateCall()
    {
        var (db, job, client) = CreateJob();
        var (_, invoice) = SeedInvoice(db, xeroContactId: "already-known-contact");
        SeedOutboxItem(db, XeroSyncAction.PushInvoice, invoice.Id);

        await job.PushDueItemsAsync(CancellationToken.None);

        Assert.Empty(client.CreatedContacts);
        Assert.Equal("already-known-contact", client.PushedInvoices[0].ContactId);
    }

    [Fact]
    public async Task PushInvoice_ExistingXeroInvoiceId_PushesAsUpdate()
    {
        var (db, job, client) = CreateJob();
        var (_, invoice) = SeedInvoice(db, xeroContactId: "contact-1", xeroInvoiceId: "existing-invoice-1");
        SeedOutboxItem(db, XeroSyncAction.PushInvoice, invoice.Id);

        await job.PushDueItemsAsync(CancellationToken.None);

        Assert.Equal("existing-invoice-1", client.PushedInvoices[0].ExistingXeroInvoiceId);
    }

    [Fact]
    public async Task VoidInvoice_WhenPushedBefore_CallsVoid_AndMarksVoided()
    {
        var (db, job, client) = CreateJob();
        var (_, invoice) = SeedInvoice(db, xeroInvoiceId: "invoice-to-void");
        SeedOutboxItem(db, XeroSyncAction.VoidInvoice, invoice.Id);

        await job.PushDueItemsAsync(CancellationToken.None);

        Assert.Equal(["invoice-to-void"], client.VoidedInvoiceIds);
        var updated = await db.Invoices.FirstAsync(i => i.Id == invoice.Id);
        Assert.Equal(XeroInvoiceSyncStatus.Voided, updated.XeroSyncStatus);
        Assert.Empty(await db.XeroSyncItems.ToListAsync());
    }

    [Fact]
    public async Task VoidInvoice_NeverPushed_NoClientCall_JustRemovesRow()
    {
        var (db, job, client) = CreateJob();
        var (_, invoice) = SeedInvoice(db); // XeroInvoiceId null
        SeedOutboxItem(db, XeroSyncAction.VoidInvoice, invoice.Id);

        await job.PushDueItemsAsync(CancellationToken.None);

        Assert.Empty(client.VoidedInvoiceIds);
        Assert.Empty(await db.XeroSyncItems.ToListAsync());
    }

    [Fact]
    public async Task PushInvoice_Failure_RecordsErrorOnOutboxAndInvoice_SchedulesBackoff()
    {
        var (db, job, client) = CreateJob();
        var (_, invoice) = SeedInvoice(db);
        var item = SeedOutboxItem(db, XeroSyncAction.PushInvoice, invoice.Id);
        var originalNextAttemptUtc = item.NextAttemptUtc;
        client.EnqueuePushFailure(new InvalidOperationException("Xero returned 500"));

        await job.PushDueItemsAsync(CancellationToken.None);

        var updatedItem = await db.XeroSyncItems.SingleAsync();
        Assert.Equal(1, updatedItem.Attempts);
        Assert.Equal("Xero returned 500", updatedItem.LastError);
        Assert.True(updatedItem.NextAttemptUtc > originalNextAttemptUtc);

        var updatedInvoice = await db.Invoices.FirstAsync(i => i.Id == invoice.Id);
        Assert.Equal(XeroInvoiceSyncStatus.Failed, updatedInvoice.XeroSyncStatus);
        Assert.Equal("Xero returned 500", updatedInvoice.XeroLastError);
    }

    [Fact]
    public async Task PushDueItemsAsync_XeroNotConfigured_NoOps()
    {
        var (db, job, client) = CreateJob(xeroConfigured: false);
        var (_, invoice) = SeedInvoice(db);
        SeedOutboxItem(db, XeroSyncAction.PushInvoice, invoice.Id);

        await job.PushDueItemsAsync(CancellationToken.None);

        Assert.Empty(client.PushedInvoices);
        Assert.Single(await db.XeroSyncItems.ToListAsync()); // left untouched, not dropped
    }

    [Fact]
    public async Task PushDueItemsAsync_NoDefaultAccountCode_NoOps()
    {
        var (db, job, client) = CreateJob(defaultAccountCode: null);
        var (_, invoice) = SeedInvoice(db);
        SeedOutboxItem(db, XeroSyncAction.PushInvoice, invoice.Id);

        await job.PushDueItemsAsync(CancellationToken.None);

        Assert.Empty(client.PushedInvoices);
        Assert.Single(await db.XeroSyncItems.ToListAsync());
    }
}
