using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>Shape returned to API clients. Includes the resolved title/type names for convenience.</summary>
public record EmployeeDto(
    Guid Id,
    string FirstName,
    string LastName,
    Guid TitleId,
    string? TitleName,
    Guid EmploymentTypeId,
    string? EmploymentTypeName,
    string ContactNumber,
    string EmailAddress,
    string PhysicalAddress,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string? UpdatedByName);

/// <summary>Payload for creating an employee. <c>TitleId</c> and <c>EmploymentTypeId</c> must reference existing records.</summary>
public record CreateEmployeeRequest(
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [Required] Guid TitleId,
    [Required] Guid EmploymentTypeId,
    [Required, Phone, MaxLength(30)] string ContactNumber,
    [Required, EmailAddress, MaxLength(200)] string EmailAddress,
    [Required, MaxLength(500)] string PhysicalAddress);

/// <summary>Payload for updating an existing employee.</summary>
public record UpdateEmployeeRequest(
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [Required] Guid TitleId,
    [Required] Guid EmploymentTypeId,
    [Required, Phone, MaxLength(30)] string ContactNumber,
    [Required, EmailAddress, MaxLength(200)] string EmailAddress,
    [Required, MaxLength(500)] string PhysicalAddress);
