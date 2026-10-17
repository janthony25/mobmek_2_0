using System.Net;
using System.Net.Http.Json;
using MobmekApi.Services;

namespace MobmekApi.Tests.Services;

/// <summary>Covers <see cref="XeroAccountingClient"/>'s request/response mapping against a
/// stubbed <see cref="HttpMessageHandler"/> (reuses <see cref="StubXeroHttpMessageHandler"/> from
/// <c>XeroAuthServiceTests</c> — same assembly, no real Xero credentials needed).</summary>
public class XeroAccountingClientTests
{
    private static (XeroAccountingClient Client, StubXeroHttpMessageHandler Handler) Build()
    {
        var handler = new StubXeroHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.xero.com/api.xro/2.0/") };
        return (new XeroAccountingClient(httpClient), handler);
    }

    [Fact]
    public async Task FindContactIdByEmailAsync_Found_ReturnsContactId()
    {
        var (client, handler) = Build();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { Contacts = new[] { new { ContactID = "contact-1", Name = "Jane Doe" } } }),
        });

        var result = await client.FindContactIdByEmailAsync("token", "tenant-1", "jane@example.com");

        Assert.Equal("contact-1", result);
        var request = handler.Requests.Single();
        Assert.Equal("tenant-1", request.Headers.GetValues("Xero-tenant-id").Single());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Contains("Contacts?where=", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task FindContactIdByEmailAsync_NotFound_ReturnsNull()
    {
        var (client, handler) = Build();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { Contacts = Array.Empty<object>() }) });

        var result = await client.FindContactIdByEmailAsync("token", "tenant-1", "nobody@example.com");

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateContactAsync_ReturnsNewContactId()
    {
        var (client, handler) = Build();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { Contacts = new[] { new { ContactID = "contact-new", Name = "Jane Doe" } } }),
        });

        var result = await client.CreateContactAsync("token", "tenant-1", "Jane Doe", "jane@example.com");

        Assert.Equal("contact-new", result);
        Assert.Equal(HttpMethod.Post, handler.Requests.Single().Method);
    }

    [Fact]
    public async Task PushInvoiceAsync_NoExistingId_CreatesAuthorisedInvoice_ExclusiveLineAmounts()
    {
        var (client, handler) = Build();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { Invoices = new[] { new { InvoiceID = "invoice-new" } } }),
        });

        var request = new XeroPushInvoiceRequest(
            ExistingXeroInvoiceId: null, ContactId: "contact-1", InvoiceNumber: "INV-0001",
            IssueDate: new DateOnly(2026, 10, 17), DueDate: new DateOnly(2026, 11, 1), Reference: "Brakes",
            Lines: [new XeroInvoiceLinePayload("Pads", 1, 110m, "200", "OUTPUT2")]);

        var result = await client.PushInvoiceAsync("token", "tenant-1", request);

        Assert.Equal("invoice-new", result.XeroInvoiceId);
        var body = handler.RequestBodies.Single();
        Assert.Contains("\"Status\":\"AUTHORISED\"", body);
        Assert.Contains("\"LineAmountTypes\":\"Exclusive\"", body);
        Assert.Contains("\"Type\":\"ACCREC\"", body);
        Assert.DoesNotContain("\"InvoiceID\"", body); // omitted entirely when creating
    }

    [Fact]
    public async Task PushInvoiceAsync_WithExistingId_IncludesInvoiceIDForUpdate()
    {
        var (client, handler) = Build();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { Invoices = new[] { new { InvoiceID = "invoice-existing" } } }),
        });

        var request = new XeroPushInvoiceRequest(
            ExistingXeroInvoiceId: "invoice-existing", ContactId: "contact-1", InvoiceNumber: "INV-0001",
            IssueDate: new DateOnly(2026, 10, 17), DueDate: null, Reference: "Brakes",
            Lines: [new XeroInvoiceLinePayload("Pads", 1, 110m, "200", "OUTPUT2")]);

        await client.PushInvoiceAsync("token", "tenant-1", request);

        Assert.Contains("\"InvoiceID\":\"invoice-existing\"", handler.RequestBodies.Single());
    }

    [Fact]
    public async Task VoidInvoiceAsync_PostsVoidedStatus()
    {
        var (client, handler) = Build();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { Invoices = new[] { new { InvoiceID = "invoice-1" } } }),
        });

        await client.VoidInvoiceAsync("token", "tenant-1", "invoice-1");

        var body = handler.RequestBodies.Single();
        Assert.Contains("\"InvoiceID\":\"invoice-1\"", body);
        Assert.Contains("\"Status\":\"VOIDED\"", body);
    }

    [Fact]
    public async Task ListActiveRevenueAccountsAsync_MapsAndFiltersAccountsWithNoCode()
    {
        var (client, handler) = Build();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                Accounts = new[]
                {
                    new { Code = (string?)"200", Name = "Sales", Type = "REVENUE", Status = "ACTIVE" },
                    new { Code = (string?)null, Name = "No Code Account", Type = "REVENUE", Status = "ACTIVE" },
                },
            }),
        });

        var result = await client.ListActiveRevenueAccountsAsync("token", "tenant-1");

        Assert.Single(result);
        Assert.Equal(new XeroAccountSummary("200", "Sales"), result[0]);
    }

    [Fact]
    public async Task NonSuccessStatus_ThrowsWithBodyInMessage()
    {
        var (client, handler) = Build();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{\"Message\":\"invalid_scope\"}") });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.FindContactIdByEmailAsync("token", "tenant-1", "x@y.com"));
        Assert.Contains("invalid_scope", ex.Message);
    }
}
