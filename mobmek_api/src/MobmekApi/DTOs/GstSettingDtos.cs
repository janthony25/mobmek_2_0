using System.ComponentModel.DataAnnotations;

namespace MobmekApi.DTOs;

/// <summary>The current GST configuration. <c>Rate</c> is a fraction (0.15 = 15%).</summary>
public record GstSettingDto(Guid Id, decimal Rate, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc, string? UpdatedByName);

/// <summary>Payload for changing the GST rate. <c>Rate</c> is a fraction between 0 and 1 (0.15 = 15%).</summary>
public record UpdateGstSettingRequest([Range(0, 1)] decimal Rate);
