using MobmekApi.DTOs;

namespace MobmekApi.Services;

/// <summary>
/// Reads and updates the single GST configuration row. The row is created on demand the first
/// time it is read, defaulting to 15%, so callers never have to seed it.
/// </summary>
public interface IGstSettingService
{
    Task<GstSettingDto> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task<GstSettingDto> UpdateAsync(decimal rate, CancellationToken cancellationToken = default);
}
