namespace MobmekApi;

/// <summary>Named CORS policies, shared between <c>Program.cs</c> and the controllers.</summary>
public static class CorsPolicies
{
    /// <summary>
    /// Allows the public marketing site's origin to call the anonymous booking endpoints.
    /// Origins come from configuration (<c>PublicSite:AllowedOrigins</c>) — never wildcarded,
    /// since these endpoints write to the workshop calendar.
    /// </summary>
    public const string PublicSite = "PublicSite";
}

/// <summary>Named rate-limiting policies for the anonymous endpoints.</summary>
public static class RateLimitPolicies
{
    /// <summary>Availability reads: cheap and idempotent, so a generous ceiling.</summary>
    public const string PublicBookingRead = "public-booking-read";

    /// <summary>
    /// Booking writes: deliberately tight. Without a login or a captcha in front of it, this
    /// per-IP limit is what stops someone filling the calendar with junk requests.
    /// </summary>
    public const string PublicBookingWrite = "public-booking-write";
}
