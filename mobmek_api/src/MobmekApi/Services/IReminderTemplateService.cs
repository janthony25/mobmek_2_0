using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface IReminderTemplateService
{
    Task<IReadOnlyList<ReminderTemplateDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ReminderTemplateDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ReminderTemplateDto> CreateAsync(CreateReminderTemplateRequest request, CancellationToken cancellationToken = default);

    Task<ReminderTemplateDto?> UpdateAsync(Guid id, UpdateReminderTemplateRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
