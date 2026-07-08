using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface IAuthService
{
    /// <summary>Builds the current-user shape (employee name + roles) for an Identity user id.
    /// Null if the user or its linked employee no longer exists.</summary>
    Task<CurrentUserDto?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
