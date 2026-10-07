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

    /// <summary>Unauthenticated counterpart to <see cref="RequestPasswordChangeCodeAsync"/>, for a
    /// user who's locked out and can't sign in to reach the normal flow. Always returns
    /// <see cref="AccountError.None"/> unless the system itself isn't configured to send email —
    /// never <see cref="AccountError.NotConfigured"/>-vs-"unknown email", so an anonymous caller
    /// can't use this to probe whether an email has an account. No code is actually sent when the
    /// email doesn't match an active, email-confirmed account; that's indistinguishable from the
    /// caller's point of view.</summary>
    Task<AccountError> RequestForgotPasswordCodeAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Unauthenticated counterpart to <see cref="ConfirmPasswordChangeAsync"/> — looks the
    /// user up by email instead of trusting a session.</summary>
    Task<(AccountError Error, string? ErrorMessage)> ResetForgottenPasswordAsync(
        ResetForgottenPasswordRequest request, CancellationToken cancellationToken = default);
}
