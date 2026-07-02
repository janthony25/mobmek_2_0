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
/// auto-posted from an invoice payment (read-only here; <see cref="JobId"/> lets the UI link
/// back to the source job); rows with <see cref="TransferGroupId"/>/<see cref="SplitGroupId"/>
/// set are transfer legs / split lines managed as a group. <see cref="RunningBalance"/> is
/// populated only on paged lists scoped to a single account.
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
    Guid? PayeeId,
    string? Counterparty,
    string Status,
    Guid? InvoiceId,
    Guid? JobId,
    Guid? TransferGroupId,
    Guid? SplitGroupId,
    string GstTreatment,
    string? Notes,
    IReadOnlyList<TransactionAttachmentDto> Attachments,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    decimal? RunningBalance = null);

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
    Guid? PayeeId,
    string? Direction,
    string? Status,
    DateOnly? From,
    DateOnly? To,
    string? Search,
    int Page = 1,
    int PageSize = 50,
    Guid? SplitGroupId = null);

/// <summary>
/// Payload for recording a cash movement. <see cref="GstTreatment"/> defaults to the
/// category's default when omitted; <see cref="Status"/> defaults to "Cleared" ("Pending" is
/// the only other manual value — "Reconciled" is set exclusively by reconciliation).
/// When <see cref="PayeeId"/> is set the payee's name becomes the counterparty text.
/// </summary>
public record CreateCashTransactionRequest(
    Guid AccountId,
    [Required, MaxLength(10)] string Direction,
    decimal Amount,
    DateOnly Date,
    [Required, MaxLength(500)] string Description,
    Guid CategoryId,
    Guid? PayeeId,
    [MaxLength(200)] string? Counterparty,
    [MaxLength(20)] string? GstTreatment,
    [MaxLength(15)] string? Status,
    [MaxLength(2000)] string? Notes);

public record UpdateCashTransactionRequest(
    Guid AccountId,
    [Required, MaxLength(10)] string Direction,
    decimal Amount,
    DateOnly Date,
    [Required, MaxLength(500)] string Description,
    Guid CategoryId,
    Guid? PayeeId,
    [MaxLength(200)] string? Counterparty,
    [MaxLength(20)] string? GstTreatment,
    [MaxLength(15)] string? Status,
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

/// <summary>One line of a split payment: its own amount/category, optionally its own text.</summary>
public record SplitTransactionLine(
    decimal Amount,
    Guid CategoryId,
    [MaxLength(20)] string? GstTreatment,
    [MaxLength(500)] string? Description);

/// <summary>
/// One real-world payment covering several categories, stored as sibling rows sharing a
/// split group (same account/date/direction/payee; each line its own amount/category).
/// Needs at least two lines — one line is just a normal transaction.
/// </summary>
public record CreateSplitTransactionRequest(
    Guid AccountId,
    [Required, MaxLength(10)] string Direction,
    DateOnly Date,
    [Required, MaxLength(500)] string Description,
    Guid? PayeeId,
    [MaxLength(200)] string? Counterparty,
    [MaxLength(15)] string? Status,
    [MaxLength(2000)] string? Notes,
    IReadOnlyList<SplitTransactionLine> Lines);

/// <summary>
/// Replaces a split group's rows wholesale (attachments on replaced rows are removed with them).
/// </summary>
public record UpdateSplitTransactionRequest(
    Guid AccountId,
    [Required, MaxLength(10)] string Direction,
    DateOnly Date,
    [Required, MaxLength(500)] string Description,
    Guid? PayeeId,
    [MaxLength(200)] string? Counterparty,
    [MaxLength(15)] string? Status,
    [MaxLength(2000)] string? Notes,
    IReadOnlyList<SplitTransactionLine> Lines);

/// <summary>
/// A bulk ledger operation. <see cref="Action"/> is "SetCategory" (needs
/// <see cref="CategoryId"/>), "SetStatus" (needs <see cref="Status"/>, "Pending"/"Cleared"
/// only) or "Delete". Managed rows (invoice-posted, transfer legs, split lines), reconciled
/// rows and period-locked rows are skipped, never failed — the result reports each skip.
/// </summary>
public record BulkCashTransactionRequest(
    IReadOnlyList<Guid> Ids,
    [Required, MaxLength(20)] string Action,
    Guid? CategoryId,
    [MaxLength(15)] string? Status);

public record BulkSkippedRowDto(Guid Id, string Reason);

/// <summary>Outcome of a bulk operation: how many rows changed and exactly why the rest didn't.</summary>
public record BulkCashTransactionResultDto(int UpdatedCount, IReadOnlyList<BulkSkippedRowDto> Skipped);

/// <summary>Why a cash-transaction write was refused.</summary>
public enum CashTransactionWriteError
{
    None,
    NotFound,
    AccountNotFound,
    AccountArchived,
    CategoryNotFound,
    PayeeNotFound,
    PayeeArchived,
    InvalidDirection,
    InvalidGstTreatment,
    InvalidStatus,
    DirectionMismatchesCategory,
    NonPositiveAmount,
    InvoiceLinkedReadOnly,
    TransferLegReadOnly,
    SplitLineReadOnly,
    ReconciledReadOnly,
    PeriodLocked,
    SameAccountTransfer,
    SplitNeedsTwoLines,
    InvalidBulkAction,
}
