using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>
/// Shape returned to API clients. <c>CustomerName</c>/<c>CarLabel</c>/<c>ReminderTemplateName</c>
/// are resolved via joins for display.
/// </summary>
public record ReminderDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    Guid? CarId,
    string? CarLabel,
    Guid? ReminderTemplateId,
    string? ReminderTemplateName,
    string Title,
    DateOnly DueDate,
    bool IsDone,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Payload for creating a reminder.</summary>
public record CreateReminderRequest(
    [Required] Guid CustomerId,
    Guid? CarId,
    Guid? ReminderTemplateId,
    [Required, MaxLength(200)] string Title,
    [Required] DateOnly DueDate,
    bool IsDone,
    [MaxLength(2000)] string? Notes);

/// <summary>Payload for updating a reminder. The customer is fixed once created.</summary>
public record UpdateReminderRequest(
    Guid? CarId,
    Guid? ReminderTemplateId,
    [Required, MaxLength(200)] string Title,
    [Required] DateOnly DueDate,
    bool IsDone,
    [MaxLength(2000)] string? Notes);
