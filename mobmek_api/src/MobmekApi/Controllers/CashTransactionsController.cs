using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/cash-transactions")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
public class CashTransactionsController(ICashTransactionService transactionService) : ControllerBase
{
    private const long MaxAttachmentBytes = 10 * 1024 * 1024;

    /// <summary>
    /// One page of the ledger, newest first, with filter-wide in/out totals. Rows carry a
    /// running balance when the filter is scoped to one account (no category/payee/direction/
    /// status/search thinning).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(CashTransactionPageDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CashTransactionPageDto>> GetPaged(
        [FromQuery] Guid? accountId, [FromQuery] Guid? categoryId, [FromQuery] Guid? payeeId,
        [FromQuery] string? direction, [FromQuery] string? status,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? search,
        [FromQuery] Guid? splitGroupId = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await transactionService.GetPagedAsync(
            new CashTransactionFilter(accountId, categoryId, payeeId, direction, status, from, to, search, page, pageSize, splitGroupId), cancellationToken);
        return Ok(result);
    }

    /// <summary>Everything matching the filter as a CSV download.</summary>
    [HttpGet("export")]
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export(
        [FromQuery] Guid? accountId, [FromQuery] Guid? categoryId, [FromQuery] Guid? payeeId,
        [FromQuery] string? direction, [FromQuery] string? status,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? search,
        CancellationToken cancellationToken = default)
    {
        var csv = await transactionService.ExportCsvAsync(
            new CashTransactionFilter(accountId, categoryId, payeeId, direction, status, from, to, search), cancellationToken);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv",
            $"cash-transactions-{DateTime.UtcNow:yyyyMMdd}.csv");
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

    /// <summary>Records one payment split across two or more categories; returns the sibling lines.</summary>
    [HttpPost("split")]
    [ProducesResponseType(typeof(IReadOnlyList<CashTransactionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<CashTransactionDto>>> CreateSplit(
        CreateSplitTransactionRequest request, CancellationToken cancellationToken)
    {
        var (lines, error) = await transactionService.CreateSplitAsync(request, cancellationToken);
        if (error != CashTransactionWriteError.None)
        {
            return MapError(error);
        }

        return Created(string.Empty, lines);
    }

    /// <summary>Replaces a split group's lines wholesale (attachments on replaced lines go with them).</summary>
    [HttpPut("split/{splitGroupId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<CashTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IReadOnlyList<CashTransactionDto>>> UpdateSplit(
        Guid splitGroupId, UpdateSplitTransactionRequest request, CancellationToken cancellationToken)
    {
        var (lines, error) = await transactionService.UpdateSplitAsync(splitGroupId, request, cancellationToken);
        return error == CashTransactionWriteError.None ? Ok(lines) : MapError(error);
    }

    /// <summary>
    /// Applies one action ("SetCategory", "SetStatus" or "Delete") to many rows. Protected
    /// rows are skipped and reported with reasons rather than failing the batch.
    /// </summary>
    [HttpPost("bulk")]
    [ProducesResponseType(typeof(BulkCashTransactionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BulkCashTransactionResultDto>> Bulk(
        BulkCashTransactionRequest request, CancellationToken cancellationToken)
    {
        var (result, error) = await transactionService.BulkAsync(request, cancellationToken);
        return error == CashTransactionWriteError.None ? Ok(result) : MapError(error);
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
        CashTransactionWriteError.PayeeNotFound => BadRequest("The payee does not exist."),
        CashTransactionWriteError.PayeeArchived => BadRequest("The payee is archived."),
        CashTransactionWriteError.InvalidStatus => BadRequest("Status must be \"Pending\" or \"Cleared\" (reconciliation sets \"Reconciled\")."),
        CashTransactionWriteError.SplitNeedsTwoLines => BadRequest("A split needs at least two lines."),
        CashTransactionWriteError.InvalidBulkAction => BadRequest("Action must be \"SetCategory\", \"SetStatus\" or \"Delete\" (with its required field)."),
        CashTransactionWriteError.InvoiceLinkedReadOnly => Conflict("This row was posted from an invoice payment; correct it from the invoice."),
        CashTransactionWriteError.TransferLegReadOnly => Conflict("Transfer legs can't be edited; delete the transfer and recreate it."),
        CashTransactionWriteError.SplitLineReadOnly => Conflict("Split lines are edited as a group; use the split endpoints."),
        CashTransactionWriteError.ReconciledReadOnly => Conflict("Reconciled rows are immutable; reverse and re-enter instead."),
        CashTransactionWriteError.PeriodLocked => Conflict("That date falls in a locked period; move the lock date in settings first."),
        _ => BadRequest(),
    };
}
