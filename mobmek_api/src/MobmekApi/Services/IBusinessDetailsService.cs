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

    /// <summary>Replaces the logo, deleting any previously stored one.</summary>
    Task<BusinessDetailsDto> UploadLogoAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Opens the stored logo for reading, or null when none is set.</summary>
    Task<(string FileName, string ContentType, Stream Content)?> GetLogoAsync(CancellationToken cancellationToken = default);

    /// <summary>Removes the stored logo, if any. Returns false when there was none to remove.</summary>
    Task<bool> DeleteLogoAsync(CancellationToken cancellationToken = default);
}
