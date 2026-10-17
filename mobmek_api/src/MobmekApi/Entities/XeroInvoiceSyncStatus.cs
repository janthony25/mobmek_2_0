namespace MobmekApi.Entities;

/// <summary>Where a given <see cref="Invoice"/> stands with its Xero mirror.</summary>
public enum XeroInvoiceSyncStatus
{
    /// <summary>Never attempted (Xero was never connected, or this is a Quotation — out of
    /// scope for v1) or genuinely never pushed.</summary>
    NotSynced,

    /// <summary>An outbox row exists and hasn't succeeded yet.</summary>
    Pending,

    /// <summary><see cref="Invoice.XeroInvoiceId"/> is set and the last push succeeded.</summary>
    Synced,

    /// <summary>The last attempt failed — see <see cref="Invoice.XeroLastError"/>.</summary>
    Failed,

    /// <summary>Voided in Xero following a local reject.</summary>
    Voided,
}
