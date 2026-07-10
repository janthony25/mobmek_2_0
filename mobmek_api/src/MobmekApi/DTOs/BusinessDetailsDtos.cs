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
    /// <summary>URL to fetch the uploaded logo from (<c>GET /api/business-details/logo</c>), or null when none is set.</summary>
    string? LogoUrl,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string? UpdatedByName);

/// <summary>Payload for updating the workshop's letterhead details. The logo is set separately via the upload endpoint.</summary>
public record UpdateBusinessDetailsRequest(
    [Required, MaxLength(200)] string Name,
    [MaxLength(500)] string? Address,
    [MaxLength(255)] string? Email,
    [MaxLength(50)] string? BusinessPhone,
    [MaxLength(50)] string? Telephone,
    [MaxLength(50)] string? GstNumber,
    [MaxLength(255)] string? Website,
    [MaxLength(1000)] string? BankDetails);
