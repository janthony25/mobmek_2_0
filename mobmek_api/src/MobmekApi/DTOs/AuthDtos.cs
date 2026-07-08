using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

/// <summary>The signed-in staff member, as returned by login and the session-check endpoint.</summary>
public record CurrentUserDto(
    Guid Id,
    string Email,
    Guid EmployeeId,
    string FirstName,
    string LastName,
    IReadOnlyList<string> Roles);
