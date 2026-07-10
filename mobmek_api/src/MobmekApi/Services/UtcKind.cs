namespace MobmekApi.Services;

/// <summary>
/// Normalizes caller-supplied DateTimes before they reach an Npgsql timestamptz comparison.
/// Query-string model binding yields Unspecified kind for bare values ("2025-01-01") and
/// Local for offset values — Npgsql rejects Unspecified outright and would silently shift
/// Local by the server timezone. Unspecified is taken as already-UTC (every date field in
/// this API is a *Utc field); Local is converted.
/// </summary>
public static class UtcKind
{
    public static DateTime? Normalize(DateTime? value) => value is null ? null : Normalize(value.Value);

    public static DateTime Normalize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
