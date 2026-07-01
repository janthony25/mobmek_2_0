using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>The workshop's letterhead details, shown on generated invoices.</summary>
public record BusinessDetailsDto(
    Guid Id,
    string Name,
    string? Address,
    string? Phone,
    string? Email,
    string? Abn,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Payload for updating the workshop's letterhead details.</summary>
public record UpdateBusinessDetailsRequest(
    [Required, MaxLength(200)] string Name,
    [MaxLength(500)] string? Address,
    [MaxLength(50)] string? Phone,
    [MaxLength(255)] string? Email,
    [MaxLength(50)] string? Abn);
