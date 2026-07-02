using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>A receipt/document attached to a cash transaction (metadata only; bytes are downloaded separately).</summary>
public record TransactionAttachmentDto(
    Guid Id,
    Guid CashTransactionId,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime CreatedAtUtc);

/// <summary>
/// One cash movement. <see cref="AccountName"/> and <see cref="CategoryName"/> are included
/// so list views don't need extra lookups. Rows with <see cref="InvoiceId"/> set were
/// auto-posted from an invoice payment and are read-only here; rows with
/// <see cref="TransferGroupId"/> set are transfer legs managed as a pair.
/// </summary>
public record CashTransactionDto(
    Guid Id,
    Guid AccountId,
    string AccountName,
    string Direction,
    decimal Amount,
    DateOnly Date,
    string Description,
    Guid CategoryId,
    string CategoryName,
    string? Counterparty,
    Guid? InvoiceId,
    Guid? TransferGroupId,
    string GstTreatment,
    string? Notes,
    IReadOnlyList<TransactionAttachmentDto> Attachments,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>One page of the ledger plus the totals of everything matching the filter (not just this page).</summary>
public record CashTransactionPageDto(
    IReadOnlyList<CashTransactionDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    decimal TotalIn,
    decimal TotalOut);

/// <summary>Filter for listing transactions; all criteria are optional and combine with AND.</summary>
public record CashTransactionFilter(
    Guid? AccountId,
    Guid? CategoryId,
    string? Direction,
    DateOnly? From,
    DateOnly? To,
    string? Search,
    int Page = 1,
    int PageSize = 50);

/// <summary>
/// Payload for recording a cash movement. <see cref="GstTreatment"/> defaults to the
/// category's default when omitted.
/// </summary>
public record CreateCashTransactionRequest(
    Guid AccountId,
    [Required, MaxLength(10)] string Direction,
    decimal Amount,
    DateOnly Date,
    [Required, MaxLength(500)] string Description,
    Guid CategoryId,
    [MaxLength(200)] string? Counterparty,
    [MaxLength(20)] string? GstTreatment,
    [MaxLength(2000)] string? Notes);

public record UpdateCashTransactionRequest(
    Guid AccountId,
    [Required, MaxLength(10)] string Direction,
    decimal Amount,
    DateOnly Date,
    [Required, MaxLength(500)] string Description,
    Guid CategoryId,
    [MaxLength(200)] string? Counterparty,
    [MaxLength(20)] string? GstTreatment,
    [MaxLength(2000)] string? Notes);

/// <summary>
/// Payload for moving money between two accounts. Creates two paired legs (Out of
/// <see cref="FromAccountId"/>, In to <see cref="ToAccountId"/>) sharing a transfer group.
/// </summary>
public record CreateTransferRequest(
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    DateOnly Date,
    [MaxLength(500)] string? Description,
    [MaxLength(2000)] string? Notes);

/// <summary>Why a cash-transaction write was refused.</summary>
public enum CashTransactionWriteError
{
    None,
    NotFound,
    AccountNotFound,
    AccountArchived,
    CategoryNotFound,
    InvalidDirection,
    InvalidGstTreatment,
    DirectionMismatchesCategory,
    NonPositiveAmount,
    InvoiceLinkedReadOnly,
    TransferLegReadOnly,
    SameAccountTransfer,
}
