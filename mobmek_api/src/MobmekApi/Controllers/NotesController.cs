using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class NotesController(INoteService noteService) : ControllerBase
{
    /// <summary>Returns all sticky notes (pinned first, then newest).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<NoteDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NoteDto>>> GetAll(CancellationToken cancellationToken)
    {
        var notes = await noteService.GetAllAsync(cancellationToken);
        return Ok(notes);
    }

    /// <summary>Returns a single note by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(NoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NoteDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var note = await noteService.GetByIdAsync(id, cancellationToken);
        return note is null ? NotFound() : Ok(note);
    }

    /// <summary>Creates a new sticky note.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(NoteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NoteDto>> Create(CreateNoteRequest request, CancellationToken cancellationToken)
    {
        var (note, error) = await noteService.CreateAsync(request, cancellationToken);
        if (error != NoteWriteError.None)
        {
            return MapError(error);
        }

        return CreatedAtAction(nameof(GetById), new { id = note!.Id }, note);
    }

    /// <summary>Updates an existing sticky note.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(NoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NoteDto>> Update(Guid id, UpdateNoteRequest request, CancellationToken cancellationToken)
    {
        var (note, error) = await noteService.UpdateAsync(id, request, cancellationToken);
        if (error != NoteWriteError.None)
        {
            return MapError(error);
        }

        return Ok(note);
    }

    /// <summary>Deletes a sticky note.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await noteService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private ActionResult MapError(NoteWriteError error) => error switch
    {
        NoteWriteError.NotFound => NotFound(),
        NoteWriteError.CustomerNotFound => Problem(detail: "Customer does not exist.", statusCode: StatusCodes.Status400BadRequest),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
    };
}
