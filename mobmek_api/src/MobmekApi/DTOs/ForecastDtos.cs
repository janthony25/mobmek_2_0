namespace MobmekApi.DTOs;

/// <summary>One day's movement in the projected balance series.</summary>
public record ForecastPointDto(
    DateOnly Date,
    decimal OpeningBalance,
    decimal In,
    decimal Out,
    decimal ClosingBalance);

/// <summary>One calendar month's rolled-up movement, closing balance is the last day of the month.</summary>
public record ForecastMonthPointDto(
    int Year,
    int Month,
    decimal In,
    decimal Out,
    decimal ClosingBalance);

/// <summary>
/// A projected daily balance series for the requested scenario. <see cref="DailyPoints"/> is
/// only populated for horizons of 90 days or less (daily resolution isn't meaningful further
/// out); <see cref="MonthlyPoints"/> always covers the full horizon. <see cref="ShortageDate"/>
/// is computed from the Expected scenario regardless of which scenario was requested, per the
/// design's rule that the shortage alert always uses Expected (Worst Case is shown as the
/// stress test, not the trigger).
/// </summary>
public record ForecastResultDto(
    int HorizonDays,
    string Scenario,
    decimal OpeningBalance,
    IReadOnlyList<ForecastPointDto> DailyPoints,
    IReadOnlyList<ForecastMonthPointDto> MonthlyPoints,
    DateOnly? ShortageDate);
