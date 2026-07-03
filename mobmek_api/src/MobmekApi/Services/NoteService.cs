using System.Linq.Expressions;
using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class NoteService(AppDbContext db) : INoteService
{
    // Inline projection so EF resolves the (optional) customer name via a join.
    private static readonly Expression<Func<Note, NoteDto>> ToDto =
        n => new NoteDto(
            n.Id,
            n.Title,
            n.Body,
            n.DueDate,
            n.Color,
            n.IsPinned,
            n.IsDone,
            n.DoneAtUtc,
            n.CustomerId,
            n.Customer == null ? null : n.Customer.FirstName + " " + n.Customer.LastName,
            n.CreatedAtUtc,
            n.UpdatedAtUtc);

    public async Task<IReadOnlyList<NoteDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Pinned first, then newest — the board's natural order.
        return await db.Notes
            .AsNoTracking()
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.CreatedAtUtc)
            .Select(ToDto)
            .ToListAsync(cancellationToken);
    }

    public async Task<NoteDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await db.Notes
            .AsNoTracking()
            .Where(n => n.Id == id)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(NoteDto? Note, NoteWriteError Error)> CreateAsync(CreateNoteRequest request, CancellationToken cancellationToken = default)
    {
        if (request.CustomerId is { } customerId
            && !await db.Customers.AnyAsync(c => c.Id == customerId, cancellationToken))
        {
            return (null, NoteWriteError.CustomerNotFound);
        }

        var note = new Note
        {
            Title = request.Title,
            Body = request.Body,
            DueDate = request.DueDate,
            Color = request.Color,
            IsPinned = request.IsPinned,
            IsDone = request.IsDone,
            DoneAtUtc = request.IsDone ? DateTime.UtcNow : null,
            CustomerId = request.CustomerId,
        };

        db.Notes.Add(note);
        await db.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(note.Id, cancellationToken), NoteWriteError.None);
    }

    public async Task<(NoteDto? Note, NoteWriteError Error)> UpdateAsync(Guid id, UpdateNoteRequest request, CancellationToken cancellationToken = default)
    {
        var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (note is null)
        {
            return (null, NoteWriteError.NotFound);
        }

        if (request.CustomerId is { } customerId
            && !await db.Customers.AnyAsync(c => c.Id == customerId, cancellationToken))
        {
            return (null, NoteWriteError.CustomerNotFound);
        }

        // Stamp when the note transitions to done (kept on later edits while still
        // done, so "done for 24h" is measured from the first completion), and clear
        // it when the note is reopened.
        if (request.IsDone && !note.IsDone)
        {
            note.DoneAtUtc = DateTime.UtcNow;
        }
        else if (!request.IsDone)
        {
            note.DoneAtUtc = null;
        }

        note.Title = request.Title;
        note.Body = request.Body;
        note.DueDate = request.DueDate;
        note.Color = request.Color;
        note.IsPinned = request.IsPinned;
        note.IsDone = request.IsDone;
        note.CustomerId = request.CustomerId;

        await db.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(note.Id, cancellationToken), NoteWriteError.None);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (note is null)
        {
            return false;
        }

        db.Notes.Remove(note);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }
}
