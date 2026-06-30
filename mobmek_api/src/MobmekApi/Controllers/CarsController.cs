using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CarsController(ICarService carService) : ControllerBase
{
    /// <summary>Returns all cars, optionally filtered by owning customer via <c>?customerId=</c>.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CarDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CarDto>>> GetAll([FromQuery] Guid? customerId, CancellationToken cancellationToken)
    {
        var cars = await carService.GetAllAsync(customerId, cancellationToken);
        return Ok(cars);
    }

    /// <summary>Returns a single car by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CarDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CarDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var car = await carService.GetByIdAsync(id, cancellationToken);
        return car is null ? NotFound() : Ok(car);
    }

    /// <summary>Creates a new car for an existing customer.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CarDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CarDto>> Create(CreateCarRequest request, CancellationToken cancellationToken)
    {
        var created = await carService.CreateAsync(request, cancellationToken);
        if (created is null)
        {
            return Problem(
                detail: $"Customer '{request.CustomerId}' does not exist.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates an existing car.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CarDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CarDto>> Update(Guid id, UpdateCarRequest request, CancellationToken cancellationToken)
    {
        var updated = await carService.UpdateAsync(id, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Deletes a car.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await carService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
