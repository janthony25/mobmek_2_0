using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>Shape returned to API clients.</summary>
public record CustomerDto(
    Guid Id,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string? EmailAddress,
    string? PhysicalAddress,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Payload for creating a customer.</summary>
public record CreateCustomerRequest(
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [Required, Phone, MaxLength(30)] string PhoneNumber,
    [EmailAddress, MaxLength(200)] string? EmailAddress,
    [MaxLength(500)] string? PhysicalAddress,
    [MaxLength(2000)] string? Notes);

/// <summary>Payload for updating an existing customer.</summary>
public record UpdateCustomerRequest(
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [Required, Phone, MaxLength(30)] string PhoneNumber,
    [EmailAddress, MaxLength(200)] string? EmailAddress,
    [MaxLength(500)] string? PhysicalAddress,
    [MaxLength(2000)] string? Notes);
