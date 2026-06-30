using System.ComponentModel.DataAnnotations;
using MobmekApi.Entities;

namespace MobmekApi.DTOs;

/// <summary>Shape returned to API clients. Money fields below the inputs are backend-computed.</summary>
public record JobItemDto(
    Guid Id,
    Guid JobId,
    string ItemName,
    decimal? TradePrice,
    decimal? RetailPrice,
    MarkupSolution MarkupSolution,
    decimal Markup,
    int ItemQuantity,
    decimal SellingPrice,
    decimal UnitProfit,
    decimal ItemTotal,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>
/// Payload for creating a job item. The backend derives SellingPrice/UnitProfit/ItemTotal.
/// When <c>TradePrice</c> is set, SellingPrice = markup applied to TradePrice; when it is null,
/// the supplied <c>SellingPrice</c> is used directly.
/// </summary>
public record CreateJobItemRequest(
    [Required] Guid JobId,
    [Required, MaxLength(200)] string ItemName,
    [Range(0, double.MaxValue)] decimal? TradePrice,
    [Range(0, double.MaxValue)] decimal? RetailPrice,
    MarkupSolution MarkupSolution,
    [Range(0, double.MaxValue)] decimal Markup,
    [Range(1, int.MaxValue)] int ItemQuantity,
    [Range(0, double.MaxValue)] decimal? SellingPrice);

/// <summary>Payload for updating a job item. The owning job cannot be changed.</summary>
public record UpdateJobItemRequest(
    [Required, MaxLength(200)] string ItemName,
    [Range(0, double.MaxValue)] decimal? TradePrice,
    [Range(0, double.MaxValue)] decimal? RetailPrice,
    MarkupSolution MarkupSolution,
    [Range(0, double.MaxValue)] decimal Markup,
    [Range(1, int.MaxValue)] int ItemQuantity,
    [Range(0, double.MaxValue)] decimal? SellingPrice);
