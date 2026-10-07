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
    /// <summary>Prefix for generated invoice numbers, e.g. "INV" in "INV-0001".</summary>
    string InvoicePrefix,
    /// <summary>Prefix for generated quotation numbers, e.g. "QUO" in "QUO-0001".</summary>
    string QuotePrefix,
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
    [MaxLength(1000)] string? BankDetails,
    [Required, RegularExpression("^[A-Z0-9]{1,10}$", ErrorMessage = "Prefix must be 1-10 uppercase letters/digits.")] string InvoicePrefix,
    [Required, RegularExpression("^[A-Z0-9]{1,10}$", ErrorMessage = "Prefix must be 1-10 uppercase letters/digits.")] string QuotePrefix);
