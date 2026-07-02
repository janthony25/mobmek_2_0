using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface ICashTransactionService
{
    /// <summary>
    /// One page of the ledger, newest first, with filter-wide in/out totals
    /// (transfer legs move balances but are excluded from those totals). When the filter is
    /// scoped to a single account (and no row-thinning filters like category/search are on),
    /// each row also carries its running account balance.
    /// </summary>
    Task<CashTransactionPageDto> GetPagedAsync(CashTransactionFilter filter, CancellationToken cancellationToken = default);

    Task<CashTransactionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Everything matching the filter as CSV (same ordering as the paged list).</summary>
    Task<string> ExportCsvAsync(CashTransactionFilter filter, CancellationToken cancellationToken = default);

    Task<(CashTransactionDto? Transaction, CashTransactionWriteError Error)> CreateAsync(CreateCashTransactionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invoice-posted rows, transfer legs and split lines are managed rows and refuse direct
    /// edits; reconciled rows are immutable; period-locked dates refuse.
    /// </summary>
    Task<(CashTransactionDto? Transaction, CashTransactionWriteError Error)> UpdateAsync(Guid id, UpdateCashTransactionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a transaction and its stored attachment files. Deleting one leg of a transfer
    /// (or one line of a split) deletes the whole group — that's how the entry is undone.
    /// Invoice-posted rows, reconciled rows and locked periods refuse.
    /// </summary>
    Task<CashTransactionWriteError> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Moves money between two accounts as paired legs under the system "Transfer" category.</summary>
    Task<(IReadOnlyList<CashTransactionDto>? Legs, CashTransactionWriteError Error)> CreateTransferAsync(CreateTransferRequest request, CancellationToken cancellationToken = default);

    /// <summary>One payment split across ≥2 categories, stored as sibling rows sharing a split group.</summary>
    Task<(IReadOnlyList<CashTransactionDto>? Lines, CashTransactionWriteError Error)> CreateSplitAsync(CreateSplitTransactionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Replaces a split group's rows wholesale (attachments on replaced rows go with them).</summary>
    Task<(IReadOnlyList<CashTransactionDto>? Lines, CashTransactionWriteError Error)> UpdateSplitAsync(Guid splitGroupId, UpdateSplitTransactionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies one action to many rows. Protected rows are skipped (never failed) and each
    /// skip is reported with its reason.
    /// </summary>
    Task<(BulkCashTransactionResultDto? Result, CashTransactionWriteError Error)> BulkAsync(BulkCashTransactionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Stores the file and attaches it; returns <c>null</c> when the transaction doesn't exist.</summary>
    Task<TransactionAttachmentDto?> AddAttachmentAsync(Guid transactionId, Stream content, string fileName, string contentType, long sizeBytes, CancellationToken cancellationToken = default);

    /// <summary>Returns the attachment metadata and an open read stream, or <c>null</c> when either id doesn't match.</summary>
    Task<(TransactionAttachmentDto Attachment, Stream Content)?> GetAttachmentAsync(Guid transactionId, Guid attachmentId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAttachmentAsync(Guid transactionId, Guid attachmentId, CancellationToken cancellationToken = default);
}
