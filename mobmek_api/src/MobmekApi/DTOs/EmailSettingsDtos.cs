using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>The current email settings. <c>ResendConfigured</c> is computed from configuration
/// presence, never persisted — the API key itself is never exposed.</summary>
public record EmailSettingsDto(
    Guid Id,
    string FromName,
    string FromAddress,
    string? ReplyToAddress,
    bool BccSelf,
    bool ResendConfigured,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record UpdateEmailSettingsRequest(
    [Required, MaxLength(200)] string FromName,
    [Required, EmailAddress, MaxLength(255)] string FromAddress,
    [EmailAddress, MaxLength(255)] string? ReplyToAddress,
    bool BccSelf);

/// <summary>Payload for the settings-page "send a test email" action.</summary>
public record SendTestEmailRequest([Required, EmailAddress] string ToAddress);
