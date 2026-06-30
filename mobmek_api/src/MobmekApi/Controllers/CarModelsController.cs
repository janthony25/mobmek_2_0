using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CarModelsController(ICarModelService carModelService) : ControllerBase
{
    /// <summary>Returns car models, optionally filtered by make via <c>?makeId=</c>.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CarModelDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CarModelDto>>> GetAll([FromQuery] Guid? makeId, CancellationToken cancellationToken)
    {
        var models = await carModelService.GetAllAsync(makeId, cancellationToken);
        return Ok(models);
    }

    /// <summary>Returns a single car model by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CarModelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CarModelDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var model = await carModelService.GetByIdAsync(id, cancellationToken);
        return model is null ? NotFound() : Ok(model);
    }

    /// <summary>Creates a new model under a make.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CarModelDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CarModelDto>> Create(CreateCarModelRequest request, CancellationToken cancellationToken)
    {
        var created = await carModelService.CreateAsync(request, cancellationToken);
        if (created is null)
        {
            return Problem(detail: $"Car make '{request.CarMakeId}' does not exist.", statusCode: StatusCodes.Status400BadRequest);
        }

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates an existing model.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CarModelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CarModelDto>> Update(Guid id, UpdateCarModelRequest request, CancellationToken cancellationToken)
    {
        var (model, makeMissing) = await carModelService.UpdateAsync(id, request, cancellationToken);
        if (model is not null)
        {
            return Ok(model);
        }

        return makeMissing
            ? Problem(detail: $"Car make '{request.CarMakeId}' does not exist.", statusCode: StatusCodes.Status400BadRequest)
            : NotFound();
    }

    /// <summary>Deletes a model.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await carModelService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
