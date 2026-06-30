using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>Shape returned to API clients. <c>TotalAmount</c> is backend-computed.</summary>
public record LabourDto(
    Guid Id,
    Guid JobId,
    decimal? Hours,
    decimal? RatePerHour,
    decimal? FixedAmount,
    decimal TotalAmount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>
/// Payload for creating a labour line. If <c>FixedAmount</c> is set it becomes the total;
/// otherwise the total is <c>Hours × RatePerHour</c>.
/// </summary>
public record CreateLabourRequest(
    [Required] Guid JobId,
    [Range(0, double.MaxValue)] decimal? Hours,
    [Range(0, double.MaxValue)] decimal? RatePerHour,
    [Range(0, double.MaxValue)] decimal? FixedAmount);

/// <summary>Payload for updating a labour line. The owning job cannot be changed.</summary>
public record UpdateLabourRequest(
    [Range(0, double.MaxValue)] decimal? Hours,
    [Range(0, double.MaxValue)] decimal? RatePerHour,
    [Range(0, double.MaxValue)] decimal? FixedAmount);
