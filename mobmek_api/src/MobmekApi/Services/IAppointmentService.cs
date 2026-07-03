using MobmekApi.DTOs;
using MobmekApi.Entities;

namespace MobmekApi.Services;

public interface IAppointmentService
{
    /// <summary>
    /// Returns appointments overlapping the given range (both optional), ordered by start,
    /// optionally filtered by status and/or assigned mechanic. <paramref name="search"/>
    /// matches title, contact name/phone, vehicle description, customer name and car rego.
    /// </summary>
    Task<IReadOnlyList<AppointmentDto>> GetAllAsync(
        DateTime? from = null,
        DateTime? to = null,
        AppointmentStatus? status = null,
        Guid? mechanicId = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<AppointmentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(AppointmentDto? Appointment, AppointmentWriteError Error)> CreateAsync(
        CreateAppointmentRequest request, CancellationToken cancellationToken = default);

    Task<(AppointmentDto? Appointment, AppointmentWriteError Error)> UpdateAsync(
        Guid id, UpdateAppointmentRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
