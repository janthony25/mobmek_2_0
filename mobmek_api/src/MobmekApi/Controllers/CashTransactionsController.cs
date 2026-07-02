using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/cash-transactions")]
[Produces("application/json")]
public class CashTransactionsController(ICashTransactionService transactionService) : ControllerBase
{
    private const long MaxAttachmentBytes = 10 * 1024 * 1024;

    /// <summary>One page of the ledger, newest first, with filter-wide in/out totals.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(CashTransactionPageDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CashTransactionPageDto>> GetPaged(
        [FromQuery] Guid? accountId, [FromQuery] Guid? categoryId, [FromQuery] string? direction,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await transactionService.GetPagedAsync(
            new CashTransactionFilter(accountId, categoryId, direction, from, to, search, page, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns a single transaction with its attachments.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CashTransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CashTransactionDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await transactionService.GetByIdAsync(id, cancellationToken);
        return transaction is null ? NotFound() : Ok(transaction);
    }

    /// <summary>Records a cash movement.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CashTransactionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CashTransactionDto>> Create(CreateCashTransactionRequest request, CancellationToken cancellationToken)
    {
        var (transaction, error) = await transactionService.CreateAsync(request, cancellationToken);
        if (error != CashTransactionWriteError.None)
        {
            return MapError(error);
        }

        return CreatedAtAction(nameof(GetById), new { id = transaction!.Id }, transaction);
    }

    /// <summary>Updates a manually recorded transaction (invoice-posted rows and transfer legs refuse).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CashTransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CashTransactionDto>> Update(Guid id, UpdateCashTransactionRequest request, CancellationToken cancellationToken)
    {
        var (transaction, error) = await transactionService.UpdateAsync(id, request, cancellationToken);
        if (error != CashTransactionWriteError.None)
        {
            return MapError(error);
        }

        return Ok(transaction);
    }

    /// <summary>Deletes a transaction (both legs when it's part of a transfer). Invoice-posted rows refuse.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var error = await transactionService.DeleteAsync(id, cancellationToken);
        return error == CashTransactionWriteError.None ? NoContent() : MapError(error);
    }

    /// <summary>Moves money between two accounts; returns the paired legs.</summary>
    [HttpPost("transfer")]
    [ProducesResponseType(typeof(IReadOnlyList<CashTransactionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<CashTransactionDto>>> CreateTransfer(CreateTransferRequest request, CancellationToken cancellationToken)
    {
        var (legs, error) = await transactionService.CreateTransferAsync(request, cancellationToken);
        if (error != CashTransactionWriteError.None)
        {
            return MapError(error);
        }

        return Created(string.Empty, legs);
    }

    /// <summary>Uploads a receipt/document (max 10 MB) onto a transaction.</summary>
    [HttpPost("{id:guid}/attachments")]
    [ProducesResponseType(typeof(TransactionAttachmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionAttachmentDto>> AddAttachment(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0 || file.Length > MaxAttachmentBytes)
        {
            return BadRequest("The file must be between 1 byte and 10 MB.");
        }

        await using var content = file.OpenReadStream();
        var attachment = await transactionService.AddAttachmentAsync(
            id, content, file.FileName, file.ContentType, file.Length, cancellationToken);

        return attachment is null
            ? NotFound()
            : CreatedAtAction(nameof(GetAttachment), new { id, attachmentId = attachment.Id }, attachment);
    }

    /// <summary>Downloads an attachment's file content.</summary>
    [HttpGet("{id:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAttachment(Guid id, Guid attachmentId, CancellationToken cancellationToken)
    {
        var result = await transactionService.GetAttachmentAsync(id, attachmentId, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        var (attachment, content) = result.Value;
        return File(content, attachment.ContentType, attachment.FileName);
    }

    /// <summary>Removes an attachment and its stored file.</summary>
    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAttachment(Guid id, Guid attachmentId, CancellationToken cancellationToken)
    {
        var deleted = await transactionService.DeleteAttachmentAsync(id, attachmentId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private ActionResult MapError(CashTransactionWriteError error) => error switch
    {
        CashTransactionWriteError.NotFound => NotFound(),
        CashTransactionWriteError.AccountNotFound => BadRequest("The account does not exist."),
        CashTransactionWriteError.AccountArchived => BadRequest("The account is archived."),
        CashTransactionWriteError.CategoryNotFound => BadRequest("The category does not exist."),
        CashTransactionWriteError.InvalidDirection => BadRequest("Direction must be \"In\" or \"Out\"."),
        CashTransactionWriteError.InvalidGstTreatment => BadRequest("GST treatment must be \"Taxable\", \"Exempt\" or \"ZeroRated\"."),
        CashTransactionWriteError.DirectionMismatchesCategory => BadRequest("The category doesn't apply to that direction."),
        CashTransactionWriteError.NonPositiveAmount => BadRequest("Amount must be greater than zero."),
        CashTransactionWriteError.SameAccountTransfer => BadRequest("A transfer needs two different accounts."),
        CashTransactionWriteError.InvoiceLinkedReadOnly => Conflict("This row was posted from an invoice payment; correct it from the invoice."),
        CashTransactionWriteError.TransferLegReadOnly => Conflict("Transfer legs can't be edited; delete the transfer and recreate it."),
        _ => BadRequest(),
    };
}
