using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>A catalog service attached to a job. <c>UnitPrice</c> is snapshotted; <c>LineTotal</c> is computed.</summary>
public record JobServiceLineDto(
    Guid Id,
    Guid JobId,
    Guid JobServiceId,
    string? ServiceName,
    decimal UnitPrice,
    decimal AdditionalAmount,
    int Quantity,
    decimal LineTotal,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Payload for attaching a catalog service to a job. Price is snapshotted from the catalog.</summary>
public record CreateJobServiceLineRequest(
    [Required] Guid JobServiceId,
    [Range(1, int.MaxValue)] int Quantity = 1,
    /// <summary>
    /// Optional surcharge added once on top of the catalog price, for services whose listed
    /// price is a baseline that can run higher on a given job.
    /// </summary>
    [Range(0, double.MaxValue)] decimal AdditionalAmount = 0);

/// <summary>Payload for updating a job's service line (quantity and additional amount).</summary>
public record UpdateJobServiceLineRequest(
    [Range(1, int.MaxValue)] int Quantity,
    [Range(0, double.MaxValue)] decimal AdditionalAmount = 0);
