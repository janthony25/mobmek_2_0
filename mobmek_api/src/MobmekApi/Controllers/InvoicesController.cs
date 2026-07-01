using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/jobs/{jobId:guid}/invoices")]
[Produces("application/json")]
public class InvoicesController(IInvoiceService invoiceService) : ControllerBase
{
    /// <summary>Returns the invoices generated for a job, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<InvoiceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InvoiceDto>>> GetAll(Guid jobId, CancellationToken cancellationToken)
    {
        var invoices = await invoiceService.GetAllAsync(jobId, cancellationToken);
        return Ok(invoices);
    }

    /// <summary>Returns a single invoice on a job.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceDto>> GetById(Guid jobId, Guid id, CancellationToken cancellationToken)
    {
        var invoice = await invoiceService.GetByIdAsync(jobId, id, cancellationToken);
        return invoice is null ? NotFound() : Ok(invoice);
    }

    /// <summary>Generates an invoice from the job's items, labour and service lines.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceDto>> Create(Guid jobId, CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        var created = await invoiceService.GenerateAsync(jobId, request, cancellationToken);
        if (created is null)
        {
            return Problem(detail: $"Job '{jobId}' does not exist.", statusCode: StatusCodes.Status404NotFound);
        }

        return CreatedAtAction(nameof(GetById), new { jobId, id = created.Id }, created);
    }

    /// <summary>Marks an invoice as rejected. The invoice is kept for the record, never deleted.</summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceDto>> Reject(Guid jobId, Guid id, CancellationToken cancellationToken)
    {
        var rejected = await invoiceService.RejectAsync(jobId, id, cancellationToken);
        return rejected is null ? NotFound() : Ok(rejected);
    }

    /// <summary>Marks an invoice as paid, recording the payment date, mode of payment, payment term, and the cash/card split.</summary>
    [HttpPost("{id:guid}/pay")]
    [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceDto>> Pay(Guid jobId, Guid id, MarkInvoicePaidRequest request, CancellationToken cancellationToken)
    {
        var paid = await invoiceService.MarkPaidAsync(jobId, id, request, cancellationToken);
        return paid is null ? NotFound() : Ok(paid);
    }
}
