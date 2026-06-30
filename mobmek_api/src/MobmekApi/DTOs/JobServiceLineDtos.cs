using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>A catalog service attached to a job. <c>UnitPrice</c> is snapshotted; <c>LineTotal</c> is computed.</summary>
public record JobServiceLineDto(
    Guid Id,
    Guid JobId,
    Guid JobServiceId,
    string? ServiceName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Payload for attaching a catalog service to a job. Price is snapshotted from the catalog.</summary>
public record CreateJobServiceLineRequest(
    [Required] Guid JobServiceId,
    [Range(1, int.MaxValue)] int Quantity = 1);

/// <summary>Payload for updating a job's service line (quantity only).</summary>
public record UpdateJobServiceLineRequest(
    [Range(1, int.MaxValue)] int Quantity);
