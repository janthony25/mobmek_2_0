namespace MobmekApi.Entities;

/// <summary>
/// A receipt or supporting document attached to a cash transaction. The file bytes live
/// behind <see cref="Services.IFileStorage"/> (local disk now, S3 later);
/// <see cref="StorageKey"/> is the provider-agnostic handle to them.
/// </summary>
public class TransactionAttachment : BaseEntity
{
    public Guid CashTransactionId { get; set; }

    public CashTransaction? CashTransaction { get; set; }

    /// <summary>Original file name as uploaded, used for download.</summary>
    public required string FileName { get; set; }

    public required string ContentType { get; set; }

    /// <summary>Provider-agnostic storage handle (a relative path locally; an object key on S3).</summary>
    public required string StorageKey { get; set; }

    public long SizeBytes { get; set; }
}
