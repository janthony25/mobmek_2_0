using MobmekApi.DTOs;
using MobmekApi.Entities;

namespace MobmekApi.Services;

public interface IAppointmentService
{
    /// <summary>
    /// Returns appointments overlapping the given range (both optional), ordered by start,
    /// optionally filtered by status, assigned mechanic and/or linked job. <paramref name="search"/>
    /// matches title, contact name/phone, vehicle description, customer name and car rego.
    /// </summary>
    Task<IReadOnlyList<AppointmentDto>> GetAllAsync(
        DateTime? from = null,
        DateTime? to = null,
        AppointmentStatus? status = null,
        Guid? mechanicId = null,
        Guid? jobId = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<AppointmentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(AppointmentDto? Appointment, AppointmentWriteError Error)> CreateAsync(
        CreateAppointmentRequest request, CancellationToken cancellationToken = default);

    Task<(AppointmentDto? Appointment, AppointmentWriteError Error)> UpdateAsync(
        Guid id, UpdateAppointmentRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Convert-on-arrival step 1: creates a customer from the phone-call contact and
    /// links it to the appointment — both in a single SaveChangesAsync, so a failure can't leave
    /// an orphaned customer record with nothing pointing back at it (the failure mode when this
    /// was two separate frontend API calls). Fails with <see cref="AppointmentConvertError.AlreadyLinkedToCustomer"/>
    /// if the appointment already has a customer.</summary>
    Task<(AppointmentDto? Appointment, AppointmentConvertError Error)> ConvertToCustomerAsync(
        Guid id, CreateCustomerRequest request, CancellationToken cancellationToken = default);

    /// <summary>Convert-on-arrival step 2: creates a car for the appointment's already-linked
    /// customer and links it to the appointment — both in a single SaveChangesAsync, same
    /// atomicity rationale as <see cref="ConvertToCustomerAsync"/>. <c>request.CustomerId</c> is
    /// ignored; the appointment's linked customer is used instead.</summary>
    Task<(AppointmentDto? Appointment, AppointmentConvertError Error)> ConvertToCarAsync(
        Guid id, CreateCarRequest request, CancellationToken cancellationToken = default);
}
