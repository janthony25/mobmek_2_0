using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>The signed-in user's own profile — contact info from their linked Employee record
/// plus their login email (read-only here; only an Admin changes it, via Employees management).</summary>
public record ProfileDto(Guid EmployeeId, string FirstName, string LastName, string ContactNumber, string PhysicalAddress, string Email);

public record UpdateProfileRequest(
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [Required, Phone, MaxLength(30)] string ContactNumber,
    [Required, MaxLength(500)] string PhysicalAddress);

public record ConfirmPasswordChangeRequest(
    [Required, StringLength(6, MinimumLength = 6)] string Code,
    [Required] string NewPassword);

/// <summary>Outcome of a password-change step that depends on state outside the request body.</summary>
public enum AccountError
{
    None,
    InvalidCode,
    CodeExpired,
    NotConfigured,
    SendFailed,
    WeakPassword,
}
