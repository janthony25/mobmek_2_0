namespace MobmekApi.Entities;

/// <summary>
/// One send attempt, from queue to terminal outcome. A row is written as <see cref="OutboundEmailStatus.Queued"/>
/// before the provider is ever called, so the trail can never miss (or misreport) a send. Retrying
/// a failed/bounced send creates a <b>new</b> row — history is never overwritten. Id and
/// <c>CreatedAtUtc</c> (from <see cref="BaseEntity"/>) double as the "queued at" timestamp.
/// </summary>
public class OutboundEmail : BaseEntity
{
    public required string ToAddress { get; set; }

    public string? ToName { get; set; }

    /// <summary>Comma-separated; small N expected.</summary>
    public string? CcAddresses { get; set; }

    public required string Subject { get; set; }

    /// <summary>Rendered snapshot of exactly what was sent.</summary>
    public required string BodyHtml { get; set; }

    public OutboundEmailStatus Status { get; set; } = OutboundEmailStatus.Queued;

    /// <summary>The provider's email id — set once accepted; drives status polling.</summary>
    public string? ProviderMessageId { get; set; }

    /// <summary>Provider error on Failed, bounce/complaint reason on Bounced/Complained.</summary>
    public string? ErrorMessage { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public DateTime? DeliveredAtUtc { get; set; }

    public DateTime? FailedAtUtc { get; set; }

    public OutboundEmailKind Kind { get; set; } = OutboundEmailKind.Invoice;

    public Guid? CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public Guid? InvoiceId { get; set; }

    public Invoice? Invoice { get; set; }
}
