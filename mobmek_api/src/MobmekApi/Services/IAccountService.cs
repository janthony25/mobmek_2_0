using MobmekApi.DTOs;

namespace MobmekApi.Services;

/// <summary>
/// Self-service account management: viewing/editing your own contact details, and changing
/// your own password via an emailed one-time code instead of your current password.
/// </summary>
public interface IAccountService
{
    /// <summary>Null if the user or its linked employee no longer exists.</summary>
    Task<ProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ProfileDto?> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);

    /// <summary>Emails a fresh 6-digit code (10-minute expiry), superseding any code already
    /// pending for this user.</summary>
    Task<AccountError> RequestPasswordChangeCodeAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Verifies the code and, if valid, resets the password without requiring the old one.</summary>
    Task<(AccountError Error, string? ErrorMessage)> ConfirmPasswordChangeAsync(
        Guid userId, ConfirmPasswordChangeRequest request, CancellationToken cancellationToken = default);
}
