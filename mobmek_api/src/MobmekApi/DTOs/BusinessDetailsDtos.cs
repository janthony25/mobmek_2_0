using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>The workshop's letterhead details, shown on generated invoices.</summary>
public record BusinessDetailsDto(
    Guid Id,
    string Name,
    string? Address,
    string? Email,
    string? BusinessPhone,
    string? Telephone,
    string? GstNumber,
    string? Website,
    string? BankDetails,
    string? LogoUrl,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Payload for updating the workshop's letterhead details.</summary>
public record UpdateBusinessDetailsRequest(
    [Required, MaxLength(200)] string Name,
    [MaxLength(500)] string? Address,
    [MaxLength(255)] string? Email,
    [MaxLength(50)] string? BusinessPhone,
    [MaxLength(50)] string? Telephone,
    [MaxLength(50)] string? GstNumber,
    [MaxLength(255)] string? Website,
    [MaxLength(1000)] string? BankDetails,
    [MaxLength(500)] string? LogoUrl);
