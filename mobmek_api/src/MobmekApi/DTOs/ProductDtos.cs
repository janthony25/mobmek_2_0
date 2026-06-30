using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>Shape returned to API clients.</summary>
public record ProductDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Payload for creating a product.</summary>
public record CreateProductRequest(
    [Required, MaxLength(200)] string Name,
    [MaxLength(2000)] string? Description,
    [Range(0, double.MaxValue)] decimal Price,
    [Range(0, int.MaxValue)] int StockQuantity);

/// <summary>Payload for updating an existing product.</summary>
public record UpdateProductRequest(
    [Required, MaxLength(200)] string Name,
    [MaxLength(2000)] string? Description,
    [Range(0, double.MaxValue)] decimal Price,
    [Range(0, int.MaxValue)] int StockQuantity);
