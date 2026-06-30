using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CarMakesController(ICarMakeService carMakeService) : ControllerBase
{
    /// <summary>Returns all car makes.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CarMakeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CarMakeDto>>> GetAll(CancellationToken cancellationToken)
    {
        var makes = await carMakeService.GetAllAsync(cancellationToken);
        return Ok(makes);
    }

    /// <summary>Returns a single car make by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CarMakeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CarMakeDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var make = await carMakeService.GetByIdAsync(id, cancellationToken);
        return make is null ? NotFound() : Ok(make);
    }

    /// <summary>Creates a new car make.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CarMakeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CarMakeDto>> Create(CreateCarMakeRequest request, CancellationToken cancellationToken)
    {
        var created = await carMakeService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates an existing car make.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CarMakeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CarMakeDto>> Update(Guid id, UpdateCarMakeRequest request, CancellationToken cancellationToken)
    {
        var updated = await carMakeService.UpdateAsync(id, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Deletes a car make.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await carMakeService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
