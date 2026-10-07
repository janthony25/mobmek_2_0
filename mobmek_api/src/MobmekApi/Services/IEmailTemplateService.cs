using MobmekApi.DTOs;

namespace MobmekApi.Services;

/// <summary>
/// Reads and updates the 3 seeded outbound-wording templates (<see cref="Entities.EmailTemplateKeys"/>).
/// Rows are created on demand the first time any of them is read, defaulting to the wording
/// baked in here, so callers (including fresh dev/prod databases) never have to run a separate
/// seed step.
/// </summary>
public interface IEmailTemplateService
{
    Task<IReadOnlyList<EmailTemplateDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Null only for an unrecognized key — every key in <see cref="Entities.EmailTemplateKeys.All"/> always exists.</summary>
    Task<EmailTemplateDto?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Null when <paramref name="key"/> isn't a recognized template key.</summary>
    Task<EmailTemplateDto?> UpdateAsync(string key, UpdateEmailTemplateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Renders the current wording against a fixed set of dummy tokens, for the editor's preview. Null for an unrecognized key.</summary>
    Task<EmailTemplatePreviewDto?> PreviewAsync(string key, CancellationToken cancellationToken = default);
}
