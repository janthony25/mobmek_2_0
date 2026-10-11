using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CustomersController(ICustomerService customerService) : ControllerBase
{
    /// <summary>Returns all customers.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CustomerDto>>> GetAll(CancellationToken cancellationToken)
    {
        var customers = await customerService.GetAllAsync(cancellationToken);
        return Ok(customers);
    }

    /// <summary>Returns one page of customers with list-card aggregates, optionally filtered/sorted.</summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<CustomerListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CustomerListItemDto>>> GetPaged(
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] DateOnly? dateFrom = null,
        [FromQuery] DateOnly? dateTo = null)
    {
        var result = await customerService.GetPagedAsync(page, pageSize, search, sortBy, dateFrom, dateTo, cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns a single customer by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var customer = await customerService.GetByIdAsync(id, cancellationToken);
        return customer is null ? NotFound() : Ok(customer);
    }

    /// <summary>Soft duplicate check for the create/edit form — looks for an existing customer
    /// with the same phone and/or email. Advisory only: returning matches never blocks a save,
    /// the caller just gets to decide whether to proceed. Pass <c>excludeId</c> when editing so
    /// the customer being edited doesn't match itself.</summary>
    [HttpGet("check-duplicate")]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerDuplicateMatchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CustomerDuplicateMatchDto>>> CheckDuplicate(
        CancellationToken cancellationToken, [FromQuery] string? phone = null, [FromQuery] string? email = null, [FromQuery] Guid? excludeId = null)
    {
        var matches = await customerService.CheckDuplicatesAsync(phone, email, excludeId, cancellationToken);
        return Ok(matches);
    }

    /// <summary>Creates a new customer.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomerDto>> Create(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var created = await customerService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Creates a new customer together with their first car, atomically — the main
    /// "Add Customer" flow, so a customer can never be created without a vehicle on file (the
    /// strongest available guard against duplicate customers, since rego is already globally
    /// unique). On a car-side error, nothing is saved, not even the customer.</summary>
    [HttpPost("with-car")]
    [ProducesResponseType(typeof(CustomerWithCarDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomerWithCarDto>> CreateWithCar(CreateCustomerWithCarRequest request, CancellationToken cancellationToken)
    {
        var (result, error) = await customerService.CreateWithCarAsync(request, cancellationToken);
        if (error != CarWriteError.None)
        {
            return MapCarError(error);
        }

        return CreatedAtAction(nameof(GetById), new { id = result!.Customer.Id }, result);
    }

    /// <summary>Updates an existing customer.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDto>> Update(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        var updated = await customerService.UpdateAsync(id, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Deletes a customer.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await customerService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private ActionResult MapCarError(CarWriteError error) => error switch
    {
        CarWriteError.MakeNotFound => Problem(detail: "Car make does not exist.", statusCode: StatusCodes.Status400BadRequest),
        CarWriteError.ModelNotFound => Problem(detail: "Car model does not exist.", statusCode: StatusCodes.Status400BadRequest),
        CarWriteError.ModelNotInMake => Problem(detail: "The selected model does not belong to the selected make.", statusCode: StatusCodes.Status400BadRequest),
        CarWriteError.DuplicateRego => Problem(detail: "A car with this rego already exists.", statusCode: StatusCodes.Status400BadRequest),
        CarWriteError.DuplicateVin => Problem(detail: "A car with this VIN already exists.", statusCode: StatusCodes.Status400BadRequest),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
    };
}
