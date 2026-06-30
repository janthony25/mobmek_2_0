using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>Shape returned to API clients.</summary>
public record CarModelDto(
    Guid Id,
    Guid CarMakeId,
    string? CarMakeName,
    string Name,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Payload for creating a model under a make.</summary>
public record CreateCarModelRequest(
    [Required] Guid CarMakeId,
    [Required, MaxLength(100)] string Name);

/// <summary>Payload for updating an existing model.</summary>
public record UpdateCarModelRequest(
    [Required] Guid CarMakeId,
    [Required, MaxLength(100)] string Name);
