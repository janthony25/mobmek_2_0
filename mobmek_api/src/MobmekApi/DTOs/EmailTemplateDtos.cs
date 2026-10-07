using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>One editable outbound-wording template.</summary>
public record EmailTemplateDto(
    Guid Id,
    string Key,
    string Name,
    string SubjectTemplate,
    string BodyIntroTemplate,
    bool IsSystem,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string? UpdatedByName);

/// <summary>Payload for editing a template's wording. The key/name are fixed.</summary>
public record UpdateEmailTemplateRequest(
    [Required, MaxLength(500)] string SubjectTemplate,
    [Required, MaxLength(4000)] string BodyIntroTemplate);

/// <summary>Rendered sample using dummy token values, so the editor can show what the wording
/// will actually look like without needing a real invoice/reminder/appointment.</summary>
public record EmailTemplatePreviewDto(string Subject, string BodyIntro);
