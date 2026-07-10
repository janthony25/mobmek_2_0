using System.ComponentModel.DataAnnotations;
using MobmekApi.Entities;

namespace MobmekApi.DTOs;

/// <summary>
/// Shape returned to API clients. Linked names are flattened for display; the soft
/// contact fields are the phone-call snapshot for appointments not yet converted.
/// </summary>
public record AppointmentDto(
    Guid Id,
    string Title,
    DateTime StartUtc,
    DateTime EndUtc,
    AppointmentStatus Status,
    string? Notes,
    string? ContactName,
    string? ContactPhone,
    string? VehicleDescription,
    Guid? CustomerId,
    string? CustomerName,
    Guid? CarId,
    string? CarDescription,
    Guid? JobId,
    string? JobTitle,
    Guid? MechanicId,
    string? MechanicName,
    string? GoogleEventId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string? UpdatedByName);

/// <summary>
/// Payload for creating an appointment. Either <c>CustomerId</c> or
/// <c>ContactName</c> + <c>ContactPhone</c> must be provided; <c>CarId</c> requires
/// <c>CustomerId</c> and must belong to that customer.
/// </summary>
public record CreateAppointmentRequest(
    [Required, MaxLength(200)] string Title,
    [Required] DateTime StartUtc,
    [Required] DateTime EndUtc,
    AppointmentStatus Status,
    [MaxLength(4000)] string? Notes,
    [MaxLength(200)] string? ContactName,
    [MaxLength(30)] string? ContactPhone,
    [MaxLength(500)] string? VehicleDescription,
    Guid? CustomerId,
    Guid? CarId,
    Guid? JobId,
    Guid? MechanicId);

/// <summary>Payload for updating an appointment; same rules as create.</summary>
public record UpdateAppointmentRequest(
    [Required, MaxLength(200)] string Title,
    [Required] DateTime StartUtc,
    [Required] DateTime EndUtc,
    AppointmentStatus Status,
    [MaxLength(4000)] string? Notes,
    [MaxLength(200)] string? ContactName,
    [MaxLength(30)] string? ContactPhone,
    [MaxLength(500)] string? VehicleDescription,
    Guid? CustomerId,
    Guid? CarId,
    Guid? JobId,
    Guid? MechanicId);

/// <summary>Why an appointment write was rejected.</summary>
public enum AppointmentWriteError
{
    None,
    NotFound,
    EndNotAfterStart,
    MissingContactOrCustomer,
    CustomerNotFound,
    CarNotFound,
    CarNotOwnedByCustomer,
    CarWithoutCustomer,
    JobNotFound,
    JobCustomerMismatch,
    MechanicNotFound,
}
