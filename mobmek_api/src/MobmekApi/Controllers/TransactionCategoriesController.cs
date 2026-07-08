using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/transaction-categories")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
public class TransactionCategoriesController(ITransactionCategoryService categoryService) : ControllerBase
{
    /// <summary>Lists transaction categories, grouped for pickers; pass <c>?includeArchived=true</c> for archived ones too.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TransactionCategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TransactionCategoryDto>>> GetAll([FromQuery] bool includeArchived, CancellationToken cancellationToken)
    {
        var categories = await categoryService.GetAllAsync(includeArchived, cancellationToken);
        return Ok(categories);
    }

    /// <summary>Returns a single category.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TransactionCategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionCategoryDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var category = await categoryService.GetByIdAsync(id, cancellationToken);
        return category is null ? NotFound() : Ok(category);
    }

    /// <summary>Creates a user category.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TransactionCategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TransactionCategoryDto>> Create(CreateTransactionCategoryRequest request, CancellationToken cancellationToken)
    {
        var (category, error) = await categoryService.CreateAsync(request, cancellationToken);
        if (error != TransactionCategoryWriteError.None)
        {
            return MapError(error);
        }

        return CreatedAtAction(nameof(GetById), new { id = category!.Id }, category);
    }

    /// <summary>Updates a category. On a system category only the name and archived flag are applied.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TransactionCategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TransactionCategoryDto>> Update(Guid id, UpdateTransactionCategoryRequest request, CancellationToken cancellationToken)
    {
        var (category, error) = await categoryService.UpdateAsync(id, request, cancellationToken);
        if (error != TransactionCategoryWriteError.None)
        {
            return MapError(error);
        }

        return Ok(category);
    }

    /// <summary>Deletes an unused user category; system or in-use categories return 409 (archive instead).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var error = await categoryService.DeleteAsync(id, cancellationToken);
        return error == TransactionCategoryWriteError.None ? NoContent() : MapError(error);
    }

    private ActionResult MapError(TransactionCategoryWriteError error) => error switch
    {
        TransactionCategoryWriteError.NotFound => NotFound(),
        TransactionCategoryWriteError.DuplicateName => Conflict("A category with that name already exists."),
        TransactionCategoryWriteError.SystemCategory => Conflict("System categories cannot be deleted."),
        TransactionCategoryWriteError.InUse => Conflict("The category has transactions; archive it instead of deleting."),
        TransactionCategoryWriteError.InvalidDirection => BadRequest("Direction must be \"In\", \"Out\" or \"Either\"."),
        TransactionCategoryWriteError.InvalidGstTreatment => BadRequest("GST treatment must be \"Taxable\", \"Exempt\" or \"ZeroRated\"."),
        _ => BadRequest(),
    };
}
