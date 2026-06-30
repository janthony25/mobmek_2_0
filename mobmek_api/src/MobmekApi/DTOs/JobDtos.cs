using System.ComponentModel.DataAnnotations;
using MobmekApi.Entities;

namespace MobmekApi.DTOs;

/// <summary>A mechanic assigned to a job.</summary>
public record JobMechanicDto(Guid EmployeeId, string FullName);

/// <summary>Shape returned to API clients. Totals are maintained by the backend.</summary>
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
    decimal TotalJobPrice,
    decimal TotalJobProfit,
    IReadOnlyList<JobMechanicDto> Mechanics,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Payload for creating a job. <c>CarId</c> must belong to <c>CustomerId</c>.</summary>
public record CreateJobRequest(
    [Required] Guid CustomerId,
    [Required] Guid CarId,
    [Required, MaxLength(200)] string Title,
    JobStatus Status,
    [Range(0, int.MaxValue)] int Odometer,
    [MaxLength(4000)] string? JobNotes,
    [MaxLength(4000)] string? InvoiceNotes);

/// <summary>Payload for updating a job. The owning customer cannot be changed.</summary>
public record UpdateJobRequest(
    [Required] Guid CarId,
    [Required, MaxLength(200)] string Title,
    JobStatus Status,
    [Range(0, int.MaxValue)] int Odometer,
    [MaxLength(4000)] string? JobNotes,
    [MaxLength(4000)] string? InvoiceNotes);

/// <summary>Payload for assigning a mechanic to a job.</summary>
public record AddJobMechanicRequest([Required] Guid EmployeeId);
