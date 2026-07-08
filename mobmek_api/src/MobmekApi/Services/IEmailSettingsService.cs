using MobmekApi.DTOs;

namespace MobmekApi.Services;

/// <summary>
/// Reads and updates the single email-settings row. The row is created on demand the first
/// time it is read, so callers never have to seed it.
/// </summary>
public interface IEmailSettingsService
{
    Task<EmailSettingsDto> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task<EmailSettingsDto> UpdateAsync(UpdateEmailSettingsRequest request, CancellationToken cancellationToken = default);
}
