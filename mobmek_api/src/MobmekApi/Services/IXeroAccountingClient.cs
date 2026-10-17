namespace MobmekApi.Services;

/// <summary>One invoice line, already mapped to Xero's shape (docs/xero-integration-design.md §4).</summary>
public record XeroInvoiceLinePayload(string Description, decimal Quantity, decimal UnitAmount, string AccountCode, string TaxType);

/// <summary>Everything needed to create-or-update one Xero invoice. <see cref="ExistingXeroInvoiceId"/>
/// null means create; set means update (Xero's POST /Invoices upserts when an id is included).</summary>
public record XeroPushInvoiceRequest(
    string? ExistingXeroInvoiceId,
    string ContactId,
    string InvoiceNumber,
    DateOnly IssueDate,
    DateOnly? DueDate,
    string Reference,
    IReadOnlyList<XeroInvoiceLinePayload> Lines);

public record XeroPushInvoiceResult(string XeroInvoiceId);

public record XeroAccountSummary(string Code, string Name);

/// <summary>
/// Thin wrapper over the Xero Accounting API (<c>api.xero.com/api.xro/2.0</c>), scoped to exactly
/// what Phase 1 needs — Contacts and Invoices (docs/xero-integration-design.md §4) plus the
/// Accounts list for the settings-page account-code dropdown (§6). Every call takes the access
/// token and tenant id explicitly rather than holding them as state, so callers (
/// <see cref="XeroSyncJob"/>) resolve a fresh, already-refreshed token via
/// <see cref="IXeroAuthService"/> each time rather than this client caching a potentially stale one.
/// </summary>
public interface IXeroAccountingClient
{
    /// <summary>Null if no Contact has this email.</summary>
    Task<string?> FindContactIdByEmailAsync(string accessToken, string tenantId, string email, CancellationToken cancellationToken = default);

    Task<string> CreateContactAsync(string accessToken, string tenantId, string name, string? email, CancellationToken cancellationToken = default);

    /// <summary>Tax-exclusive, <c>LineAmountTypes = "Exclusive"</c> — matches how this app's own
    /// invoice math works (GST added on top of the subtotal, see <c>InvoiceService.GenerateDocumentAsync</c>).
    /// Was wrongly "Inclusive" originally, silently understating every pushed invoice's Xero
    /// total by its GST amount — fixed 2026-10-17, confirmed live.</summary>
    Task<XeroPushInvoiceResult> PushInvoiceAsync(string accessToken, string tenantId, XeroPushInvoiceRequest request, CancellationToken cancellationToken = default);

    Task VoidInvoiceAsync(string accessToken, string tenantId, string xeroInvoiceId, CancellationToken cancellationToken = default);

    /// <summary>Active revenue accounts only — the candidates for the settings page's default
    /// sales account-code dropdown.</summary>
    Task<IReadOnlyList<XeroAccountSummary>> ListActiveRevenueAccountsAsync(string accessToken, string tenantId, CancellationToken cancellationToken = default);
}
