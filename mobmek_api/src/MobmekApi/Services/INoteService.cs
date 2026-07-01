using MobmekApi.DTOs;

namespace MobmekApi.Services;

/// <summary>Outcome of a note write that depends on a referenced record.</summary>
public enum NoteWriteError
{
    None,
    NotFound,
    CustomerNotFound,
}

public interface INoteService
{
    Task<IReadOnlyList<NoteDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<NoteDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(NoteDto? Note, NoteWriteError Error)> CreateAsync(CreateNoteRequest request, CancellationToken cancellationToken = default);

    Task<(NoteDto? Note, NoteWriteError Error)> UpdateAsync(Guid id, UpdateNoteRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
