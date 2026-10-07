using System.ComponentModel.DataAnnotations;
using MobmekApi.Entities;

namespace MobmekApi.DTOs;

/// <summary>A mechanic assigned to a job.</summary>
public record JobMechanicDto(Guid EmployeeId, string FullName);

/// <summary>
/// Shape returned to API clients. Totals are maintained by the backend. <c>TotalJobProfit</c>
/// is null for a non-Admin caller — see <see cref="Controllers.JobRoleRedaction"/>.
/// </summary>
public record JobDto(
    Guid Id,
    Guid CustomerId,
    string? CustomerName,
    Guid CarId,
    string? CarDescription,
    string Title,
    JobStatus Status,
    int Odometer,
    string? JobNotes,
    string? InvoiceNotes,
    DiscountType DiscountType,
    decimal DiscountValue,
    decimal TotalJobPrice,
    decimal? TotalJobProfit,
    IReadOnlyList<JobMechanicDto> Mechanics,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string? UpdatedByName);

/// <summary>Payload for creating a job. <c>CarId</c> must belong to <c>CustomerId</c>.</summary>
public record CreateJobRequest(
    [Required] Guid CustomerId,
    [Required] Guid CarId,
    [Required, MaxLength(200)] string Title,
    JobStatus Status,
    [Range(0, int.MaxValue)] int Odometer,
    [MaxLength(4000)] string? JobNotes,
    [MaxLength(4000)] string? InvoiceNotes,
    DiscountType DiscountType = DiscountType.None,
    [Range(0, 1000000)] decimal DiscountValue = 0m,
    /// <summary>
    /// When set (convert-on-arrival's final step), the appointment is linked to this job and
    /// marked <see cref="MobmekApi.Entities.AppointmentStatus.Arrived"/> in the same save as the
    /// job's creation, so the two can never end up out of sync from a failure in between.
    /// </summary>
    Guid? AppointmentId = null);

/// <summary>Payload for updating a job. The owning customer cannot be changed.</summary>
public record UpdateJobRequest(
    [Required] Guid CarId,
    [Required, MaxLength(200)] string Title,
    JobStatus Status,
    [Range(0, int.MaxValue)] int Odometer,
    [MaxLength(4000)] string? JobNotes,
    [MaxLength(4000)] string? InvoiceNotes,
    DiscountType DiscountType = DiscountType.None,
    [Range(0, 1000000)] decimal DiscountValue = 0m);

/// <summary>Payload for assigning a mechanic to a job.</summary>
public record AddJobMechanicRequest([Required] Guid EmployeeId);
