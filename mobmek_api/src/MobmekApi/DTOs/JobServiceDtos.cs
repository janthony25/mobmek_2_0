using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>Shape returned to API clients for a catalog service.</summary>
public record JobServiceDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Payload for creating a catalog service (e.g. "Oil change").</summary>
public record CreateJobServiceRequest(
    [Required, MaxLength(200)] string Name,
    [MaxLength(2000)] string? Description,
    [Range(0, double.MaxValue)] decimal Price,
    bool IsActive = true);

/// <summary>Payload for updating an existing catalog service.</summary>
public record UpdateJobServiceRequest(
    [Required, MaxLength(200)] string Name,
    [MaxLength(2000)] string? Description,
    [Range(0, double.MaxValue)] decimal Price,
    bool IsActive);
