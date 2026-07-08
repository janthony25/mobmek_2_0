using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface ILoginAttemptService
{
    /// <summary>Records one login attempt and commits it immediately (unlike the cash-flow
    /// audit trail, login has no other pending SaveChanges in the same request to ride along with).</summary>
    Task RecordAsync(
        string email, Guid? employeeId, bool succeeded, string? failureReason, string? ipAddress,
        CancellationToken cancellationToken = default);

    /// <summary>Paged trail, newest first.</summary>
    Task<LoginAttemptPageDto> GetPagedAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
}
