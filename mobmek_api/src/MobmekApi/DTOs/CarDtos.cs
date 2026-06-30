using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>Shape returned to API clients. Make/model names are resolved from the lookups.</summary>
public record CarDto(
    Guid Id,
    Guid CustomerId,
    Guid CarMakeId,
    string? CarMakeName,
    Guid CarModelId,
    string? CarModelName,
    int Year,
    string Rego,
    string? Vin,
    string? Color,
    string? EngineType,
    int? Odometer,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>
/// Payload for creating a car. <c>CustomerId</c> must exist, and <c>CarModelId</c> must
/// belong to <c>CarMakeId</c>.
/// </summary>
public record CreateCarRequest(
    [Required] Guid CustomerId,
    [Required] Guid CarMakeId,
    [Required] Guid CarModelId,
    [Range(1900, 2100)] int Year,
    [Required, MaxLength(20)] string Rego,
    [MaxLength(17)] string? Vin,
    [MaxLength(50)] string? Color,
    [MaxLength(50)] string? EngineType,
    [Range(0, int.MaxValue)] int? Odometer);

/// <summary>Payload for updating an existing car. The owning customer cannot be changed here.</summary>
public record UpdateCarRequest(
    [Required] Guid CarMakeId,
    [Required] Guid CarModelId,
    [Range(1900, 2100)] int Year,
    [Required, MaxLength(20)] string Rego,
    [MaxLength(17)] string? Vin,
    [MaxLength(50)] string? Color,
    [MaxLength(50)] string? EngineType,
    [Range(0, int.MaxValue)] int? Odometer);
