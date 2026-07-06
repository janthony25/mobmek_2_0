using MobmekApi.Entities;

namespace MobmekApi.Services;

/// <summary>Shared discount math so a job's totals and any invoice generated from it agree.</summary>
public static class DiscountCalculator
{
    /// <summary>
    /// Resolves a discount type/value against a subtotal into a dollar amount, clamped so it
    /// never exceeds the subtotal (Fixed) or 100% (Percentage).
    /// </summary>
    public static decimal ComputeAmount(DiscountType type, decimal value, decimal subtotal) => type switch
    {
        DiscountType.Fixed => Math.Clamp(value, 0m, subtotal),
        DiscountType.Percentage => Math.Round(subtotal * Math.Clamp(value, 0m, 100m) / 100m, 2, MidpointRounding.AwayFromZero),
        _ => 0m,
    };
}
