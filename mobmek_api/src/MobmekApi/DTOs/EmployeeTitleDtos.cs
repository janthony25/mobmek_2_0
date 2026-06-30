using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>Shape returned to API clients.</summary>
public record EmployeeTitleDto(
    Guid Id,
    string Name,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Payload for creating a title.</summary>
public record CreateEmployeeTitleRequest(
    [Required, MaxLength(100)] string Name);

/// <summary>Payload for updating an existing title.</summary>
public record UpdateEmployeeTitleRequest(
    [Required, MaxLength(100)] string Name);
