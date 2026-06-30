using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>Shape returned to API clients.</summary>
public record CarDto(
    Guid Id,
    Guid CustomerId,
    string Make,
    string Model,
    int Year,
    string Rego,
    string? Vin,
    string? Color,
    string? EngineType,
    int? Odometer,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Payload for creating a car. <c>CustomerId</c> must reference an existing customer.</summary>
public record CreateCarRequest(
    [Required] Guid CustomerId,
    [Required, MaxLength(100)] string Make,
    [Required, MaxLength(100)] string Model,
    [Range(1900, 2100)] int Year,
    [Required, MaxLength(20)] string Rego,
    [MaxLength(17)] string? Vin,
    [MaxLength(50)] string? Color,
    [MaxLength(50)] string? EngineType,
    [Range(0, int.MaxValue)] int? Odometer);

/// <summary>Payload for updating an existing car. The owning customer cannot be changed here.</summary>
public record UpdateCarRequest(
    [Required, MaxLength(100)] string Make,
    [Required, MaxLength(100)] string Model,
    [Range(1900, 2100)] int Year,
    [Required, MaxLength(20)] string Rego,
    [MaxLength(17)] string? Vin,
    [MaxLength(50)] string? Color,
    [MaxLength(50)] string? EngineType,
    [Range(0, int.MaxValue)] int? Odometer);
