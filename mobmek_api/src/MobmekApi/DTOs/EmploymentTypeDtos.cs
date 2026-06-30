using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>Shape returned to API clients.</summary>
public record EmploymentTypeDto(
    Guid Id,
    string Name,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Payload for creating an employment type.</summary>
public record CreateEmploymentTypeRequest(
    [Required, MaxLength(100)] string Name);

/// <summary>Payload for updating an existing employment type.</summary>
public record UpdateEmploymentTypeRequest(
    [Required, MaxLength(100)] string Name);
