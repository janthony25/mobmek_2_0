namespace MobmekApi.Entities;

/// <summary>What a <see cref="XeroSyncItem"/> row asks <see cref="Services.XeroSyncJob"/> to do.</summary>
public enum XeroSyncAction
{
    /// <summary>Create (first push) or update the invoice's Xero copy, resolving/creating its
    /// Contact first if needed. Never enqueued for a Quotation — Phase 1 is invoices only.</summary>
    PushInvoice,

    /// <summary>Void the invoice's Xero copy. A no-op (row just removed) if the invoice was never
    /// pushed in the first place — nothing to void.</summary>
    VoidInvoice,
}
