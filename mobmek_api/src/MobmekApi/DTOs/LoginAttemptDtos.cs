namespace MobmekApi.DTOs;

/// <summary>One login-attempt audit entry.</summary>
public record LoginAttemptDto(
    Guid Id,
    string Email,
    Guid? EmployeeId,
    string? EmployeeName,
    bool Succeeded,
    string? FailureReason,
    string? IpAddress,
    DateTime AttemptedAtUtc);

public record LoginAttemptPageDto(
    IReadOnlyList<LoginAttemptDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
