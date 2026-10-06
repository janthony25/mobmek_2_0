using MobmekApi.DTOs;

namespace MobmekApi.Controllers;

/// <summary>
/// Hides job-level cost/margin fields (<see cref="JobDto.TotalJobProfit"/>,
/// <see cref="JobItemDto.TradePrice"/>/<see cref="JobItemDto.Markup"/>/
/// <see cref="JobItemDto.UnitProfit"/>) from any caller without the
/// <see cref="Services.Permissions.ViewJobMargins"/> permission — today that's Admin only
/// (<c>AdminSeeder</c> grants Admin every permission), but it's checked via the permission
/// claim, not the role name, so a differently-named role could be granted it later without
/// touching this file. Techs keep full read/write access to jobs and job items; only these
/// specific fields go null for them, not the endpoints themselves.
/// </summary>
public static class JobRoleRedaction
{
    public static JobDto Redact(this JobDto dto, bool canViewMargins) =>
        canViewMargins ? dto : dto with { TotalJobProfit = null };

    public static IReadOnlyList<JobDto> Redact(this IReadOnlyList<JobDto> dtos, bool canViewMargins) =>
        canViewMargins ? dtos : dtos.Select(d => d.Redact(canViewMargins)).ToList();

    public static PagedResult<JobDto> Redact(this PagedResult<JobDto> page, bool canViewMargins) =>
        canViewMargins ? page : page with { Items = page.Items.Redact(canViewMargins) };

    public static JobItemDto Redact(this JobItemDto dto, bool canViewMargins) =>
        canViewMargins ? dto : dto with { TradePrice = null, Markup = null, UnitProfit = null };

    public static IReadOnlyList<JobItemDto> Redact(this IReadOnlyList<JobItemDto> dtos, bool canViewMargins) =>
        canViewMargins ? dtos : dtos.Select(d => d.Redact(canViewMargins)).ToList();
}
