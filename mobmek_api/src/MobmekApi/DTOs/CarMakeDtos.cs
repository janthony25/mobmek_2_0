using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>Shape returned to API clients.</summary>
public record CarMakeDto(
    Guid Id,
    string Name,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Payload for creating a car make.</summary>
public record CreateCarMakeRequest(
    [Required, MaxLength(100)] string Name);

/// <summary>Payload for updating an existing car make.</summary>
public record UpdateCarMakeRequest(
    [Required, MaxLength(100)] string Name);
