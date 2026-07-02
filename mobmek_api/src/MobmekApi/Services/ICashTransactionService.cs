using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface ICashTransactionService
{
    /// <summary>
    /// One page of the ledger, newest first, with filter-wide in/out totals
    /// (transfer legs move balances but are excluded from those totals).
    /// </summary>
    Task<CashTransactionPageDto> GetPagedAsync(CashTransactionFilter filter, CancellationToken cancellationToken = default);

    Task<CashTransactionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(CashTransactionDto? Transaction, CashTransactionWriteError Error)> CreateAsync(CreateCashTransactionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Invoice-posted rows and transfer legs are managed rows and refuse direct edits.</summary>
    Task<(CashTransactionDto? Transaction, CashTransactionWriteError Error)> UpdateAsync(Guid id, UpdateCashTransactionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a transaction and its stored attachment files. Deleting one leg of a transfer
    /// deletes both legs (that's how a transfer is undone). Invoice-posted rows refuse —
    /// they're corrected from the invoice.
    /// </summary>
    Task<CashTransactionWriteError> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Moves money between two accounts as paired legs under the system "Transfer" category.</summary>
    Task<(IReadOnlyList<CashTransactionDto>? Legs, CashTransactionWriteError Error)> CreateTransferAsync(CreateTransferRequest request, CancellationToken cancellationToken = default);

    /// <summary>Stores the file and attaches it; returns <c>null</c> when the transaction doesn't exist.</summary>
    Task<TransactionAttachmentDto?> AddAttachmentAsync(Guid transactionId, Stream content, string fileName, string contentType, long sizeBytes, CancellationToken cancellationToken = default);

    /// <summary>Returns the attachment metadata and an open read stream, or <c>null</c> when either id doesn't match.</summary>
    Task<(TransactionAttachmentDto Attachment, Stream Content)?> GetAttachmentAsync(Guid transactionId, Guid attachmentId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAttachmentAsync(Guid transactionId, Guid attachmentId, CancellationToken cancellationToken = default);
}
