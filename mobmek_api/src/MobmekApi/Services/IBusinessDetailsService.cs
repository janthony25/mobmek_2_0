using MobmekApi.DTOs;

namespace MobmekApi.Services;

/// <summary>
/// Reads and updates the single business-details (letterhead) row. The row is created on
/// demand the first time it is read, defaulting to "Mobmek Workshop", so callers never have
/// to seed it.
/// </summary>
public interface IBusinessDetailsService
{
    Task<BusinessDetailsDto> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task<BusinessDetailsDto> UpdateAsync(UpdateBusinessDetailsRequest request, CancellationToken cancellationToken = default);
}
