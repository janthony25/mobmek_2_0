using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MobmekApi.Services;

/// <summary>
/// Real <see cref="IXeroAccountingClient"/>, backed by plain <see cref="HttpClient"/> calls
/// (same hand-rolled approach as <see cref="XeroAuthService"/> — no third-party SDK). Registered
/// with a fixed <c>BaseAddress</c> of <c>https://api.xero.com/api.xro/2.0/</c> in Program.cs,
/// since every Accounting API call shares that one host+base-path.
/// </summary>
public class XeroAccountingClient(HttpClient httpClient) : IXeroAccountingClient
{
    public async Task<string?> FindContactIdByEmailAsync(
        string accessToken, string tenantId, string email, CancellationToken cancellationToken = default)
    {
        var filter = Uri.EscapeDataString($"EmailAddress==\"{email}\"");
        var response = await SendAsync<object>(HttpMethod.Get, $"Contacts?where={filter}", accessToken, tenantId, body: null, cancellationToken);
        var result = await ReadAsync<XeroContactsEnvelope>(response, cancellationToken);
        return result.Contacts?.FirstOrDefault()?.ContactID;
    }

    public async Task<string> CreateContactAsync(
        string accessToken, string tenantId, string name, string? email, CancellationToken cancellationToken = default)
    {
        var payload = new XeroContactsEnvelope([new XeroContact(null, name, email)]);
        var response = await SendAsync(HttpMethod.Post, "Contacts", accessToken, tenantId, payload, cancellationToken);
        var result = await ReadAsync<XeroContactsEnvelope>(response, cancellationToken);
        var created = result.Contacts?.FirstOrDefault()?.ContactID;
        return created ?? throw new InvalidOperationException("Xero did not return a ContactID for the newly created contact.");
    }

    public async Task<XeroPushInvoiceResult> PushInvoiceAsync(
        string accessToken, string tenantId, XeroPushInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var invoice = new XeroInvoice(
            InvoiceID: request.ExistingXeroInvoiceId,
            Type: "ACCREC",
            Contact: new XeroContactRef(request.ContactId),
            LineItems: request.Lines.Select(l => new XeroLineItem(l.Description, l.Quantity, l.UnitAmount, l.AccountCode, l.TaxType)).ToArray(),
            Date: request.IssueDate.ToString("yyyy-MM-dd"),
            DueDate: request.DueDate?.ToString("yyyy-MM-dd"),
            InvoiceNumber: request.InvoiceNumber,
            Reference: request.Reference,
            Status: "AUTHORISED",
            // Mobmek's own invoice math adds GST on top of the subtotal (InvoiceService.cs:
            // "GST is added on top of the discounted subtotal") -- item/line prices are
            // GST-exclusive, not inclusive. "Inclusive" here was wrong (confirmed live,
            // 2026-10-17: Xero showed Total = Subtotal with no GST added, a real $45-on-$345
            // understatement) and understated every invoice pushed so far by its GST amount.
            LineAmountTypes: "Exclusive");

        var payload = new XeroInvoicesEnvelope([invoice]);
        var response = await SendAsync(HttpMethod.Post, "Invoices", accessToken, tenantId, payload, cancellationToken);
        var result = await ReadAsync<XeroInvoicesEnvelope>(response, cancellationToken);
        var pushed = result.Invoices?.FirstOrDefault()?.InvoiceID;
        return pushed is not null
            ? new XeroPushInvoiceResult(pushed)
            : throw new InvalidOperationException("Xero did not return an InvoiceID for the pushed invoice.");
    }

    public async Task VoidInvoiceAsync(string accessToken, string tenantId, string xeroInvoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = new XeroInvoice(
            InvoiceID: xeroInvoiceId, Type: null, Contact: null, LineItems: null,
            Date: null, DueDate: null, InvoiceNumber: null, Reference: null, Status: "VOIDED", LineAmountTypes: null);
        var payload = new XeroInvoicesEnvelope([invoice]);
        var response = await SendAsync(HttpMethod.Post, "Invoices", accessToken, tenantId, payload, cancellationToken);
        await ReadAsync<XeroInvoicesEnvelope>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<XeroAccountSummary>> ListActiveRevenueAccountsAsync(
        string accessToken, string tenantId, CancellationToken cancellationToken = default)
    {
        var filter = Uri.EscapeDataString("Status==\"ACTIVE\"&&Type==\"REVENUE\"");
        var response = await SendAsync<object>(HttpMethod.Get, $"Accounts?where={filter}", accessToken, tenantId, body: null, cancellationToken);
        var result = await ReadAsync<XeroAccountsEnvelope>(response, cancellationToken);
        return (result.Accounts ?? [])
            .Where(a => !string.IsNullOrWhiteSpace(a.Code))
            .Select(a => new XeroAccountSummary(a.Code!, a.Name ?? a.Code!))
            .ToList();
    }

    // Omits null properties (e.g. InvoiceID on a create, or every field but InvoiceID/Status on
    // a void) rather than sending them as explicit `null`s — keeps the wire payload matching
    // exactly what each operation needs, not Xero's whole Invoice shape with blanks.
    private static readonly JsonSerializerOptions SerializerOptions = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private async Task<HttpResponseMessage> SendAsync<TBody>(
        HttpMethod method, string relativeUrl, string accessToken, string tenantId, TBody? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, relativeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Xero-tenant-id", tenantId);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: SerializerOptions);
        }

        return await httpClient.SendAsync(request, cancellationToken);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Xero Accounting API returned {(int)response.StatusCode}: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
        return result ?? throw new InvalidOperationException("Xero Accounting API returned an empty body.");
    }

    // --- Wire-format records (Xero's JSON shape) — kept private, callers use the public types above ---

    private record XeroContactsEnvelope(XeroContact[]? Contacts);

    private record XeroContact(string? ContactID, string Name, [property: JsonPropertyName("EmailAddress")] string? EmailAddress);

    private record XeroInvoicesEnvelope(XeroInvoice[]? Invoices);

    private record XeroInvoice(
        string? InvoiceID, string? Type, XeroContactRef? Contact, XeroLineItem[]? LineItems,
        string? Date, string? DueDate, string? InvoiceNumber, string? Reference, string? Status, string? LineAmountTypes);

    private record XeroContactRef(string ContactID);

    private record XeroLineItem(string Description, decimal Quantity, decimal UnitAmount, string AccountCode, string TaxType);

    private record XeroAccountsEnvelope(XeroAccount[]? Accounts);

    private record XeroAccount(string? Code, string? Name, string? Type, string? Status);
}
