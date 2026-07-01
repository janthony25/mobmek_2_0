using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>Shape returned to API clients. <c>CustomerName</c> is resolved when linked.</summary>
public record NoteDto(
    Guid Id,
    string Title,
    string? Body,
    DateOnly? DueDate,
    string? Color,
    bool IsPinned,
    bool IsDone,
    Guid? CustomerId,
    string? CustomerName,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Payload for creating a sticky note.</summary>
public record CreateNoteRequest(
    [Required, MaxLength(200)] string Title,
    [MaxLength(4000)] string? Body,
    DateOnly? DueDate,
    [MaxLength(50)] string? Color,
    bool IsPinned,
    bool IsDone,
    Guid? CustomerId);

/// <summary>Payload for updating an existing sticky note.</summary>
public record UpdateNoteRequest(
    [Required, MaxLength(200)] string Title,
    [MaxLength(4000)] string? Body,
    DateOnly? DueDate,
    [MaxLength(50)] string? Color,
    bool IsPinned,
    bool IsDone,
    Guid? CustomerId);
