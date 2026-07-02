using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface IForecastService
{
    /// <summary>
    /// Projects a daily balance series over <paramref name="horizonDays"/> (clamped to 1–366) for
    /// <paramref name="scenario"/> ("BestCase", "Expected" or "WorstCase" — anything else falls
    /// back to "Expected"). The shortage date is always computed from the Expected scenario
    /// regardless of which scenario was requested.
    /// </summary>
    Task<ForecastResultDto> ProjectAsync(int horizonDays, string? scenario, CancellationToken cancellationToken = default);
}
