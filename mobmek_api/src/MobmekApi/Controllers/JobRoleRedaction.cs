using MobmekApi.DTOs;

namespace MobmekApi.Controllers;

/// <summary>
/// Hides job-level cost/margin fields (<see cref="JobDto.TotalJobProfit"/>,
/// <see cref="JobItemDto.TradePrice"/>/<see cref="JobItemDto.Markup"/>/
/// <see cref="JobItemDto.UnitProfit"/>) from any caller that isn't Admin. No non-Admin account
/// is in use today, but the "Employee" role already exists (<c>AdminSeeder</c>) and is
/// assignable (<c>AccountAdminService</c>) — this closes the exposure before it's ever
/// exercised, rather than after. Techs keep full read/write access to jobs and job items;
/// only these specific fields go null for them, not the endpoints themselves.
/// </summary>
public static class JobRoleRedaction
{
    public static JobDto Redact(this JobDto dto, bool isAdmin) =>
        isAdmin ? dto : dto with { TotalJobProfit = null };

    public static IReadOnlyList<JobDto> Redact(this IReadOnlyList<JobDto> dtos, bool isAdmin) =>
        isAdmin ? dtos : dtos.Select(d => d.Redact(isAdmin)).ToList();

    public static PagedResult<JobDto> Redact(this PagedResult<JobDto> page, bool isAdmin) =>
        isAdmin ? page : page with { Items = page.Items.Redact(isAdmin) };

    public static JobItemDto Redact(this JobItemDto dto, bool isAdmin) =>
        isAdmin ? dto : dto with { TradePrice = null, Markup = null, UnitProfit = null };

    public static IReadOnlyList<JobItemDto> Redact(this IReadOnlyList<JobItemDto> dtos, bool isAdmin) =>
        isAdmin ? dtos : dtos.Select(d => d.Redact(isAdmin)).ToList();
}
