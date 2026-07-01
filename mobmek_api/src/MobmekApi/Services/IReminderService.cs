using MobmekApi.DTOs;

namespace MobmekApi.Services;

/// <summary>Outcome of a reminder write that depends on referenced records.</summary>
public enum ReminderWriteError
{
    None,
    NotFound,
    CustomerNotFound,
    CarNotFound,
    CarNotOwnedByCustomer,
    TemplateNotFound,
}

public interface IReminderService
{
    Task<IReadOnlyList<ReminderDto>> GetAllAsync(Guid? customerId = null, Guid? carId = null, bool includeDone = true, CancellationToken cancellationToken = default);

    Task<ReminderDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(ReminderDto? Reminder, ReminderWriteError Error)> CreateAsync(CreateReminderRequest request, CancellationToken cancellationToken = default);

    Task<(ReminderDto? Reminder, ReminderWriteError Error)> UpdateAsync(Guid id, UpdateReminderRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
