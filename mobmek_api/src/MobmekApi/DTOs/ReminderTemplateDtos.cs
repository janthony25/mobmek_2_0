using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>Shape returned to API clients.</summary>
public record ReminderTemplateDto(
    Guid Id,
    string Name,
    string? Description,
    int? DefaultIntervalMonths,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Payload for creating a reminder template.</summary>
public record CreateReminderTemplateRequest(
    [Required, MaxLength(100)] string Name,
    [MaxLength(500)] string? Description,
    [Range(1, 120)] int? DefaultIntervalMonths);

/// <summary>Payload for updating an existing reminder template.</summary>
public record UpdateReminderTemplateRequest(
    [Required, MaxLength(100)] string Name,
    [MaxLength(500)] string? Description,
    [Range(1, 120)] int? DefaultIntervalMonths);
