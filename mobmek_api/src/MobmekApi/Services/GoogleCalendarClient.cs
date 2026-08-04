using System.Net;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;

namespace MobmekApi.Services;

/// <summary>
/// Real <see cref="IGoogleCalendarClient"/> backed by <c>Google.Apis.Calendar.v3</c>. Registered as
/// a singleton; lazily initializes its <see cref="CalendarService"/> on first use (same
/// missing-config-means-disabled shape as legacy's <c>GoogleCalendarService.TryInitialize</c> and
/// <see cref="ResendEmailSender"/>'s graceful degradation) so a missing key/calendar id never
/// crashes startup — it just logs once and leaves <see cref="IsConfigured"/> false.
/// </summary>
public class GoogleCalendarClient : IGoogleCalendarClient
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleCalendarClient> _logger;
    private readonly Lock _initLock = new();

    private CalendarService? _calendarService;
    private string? _calendarId;
    private bool _initAttempted;

    public GoogleCalendarClient(IConfiguration configuration, ILogger<GoogleCalendarClient> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsConfigured
    {
        get
        {
            EnsureInitialized();
            return _calendarService is not null;
        }
    }

    public async Task<string> InsertAsync(Event googleEvent, CancellationToken cancellationToken = default)
    {
        var (service, calendarId) = RequireInitialized();
        var created = await service.Events.Insert(googleEvent, calendarId).ExecuteAsync(cancellationToken);
        return created.Id;
    }

    public async Task UpdateAsync(string eventId, Event googleEvent, CancellationToken cancellationToken = default)
    {
        var (service, calendarId) = RequireInitialized();
        await service.Events.Update(googleEvent, calendarId, eventId).ExecuteAsync(cancellationToken);
    }

    public async Task DeleteAsync(string eventId, CancellationToken cancellationToken = default)
    {
        var (service, calendarId) = RequireInitialized();
        try
        {
            await service.Events.Delete(calendarId, eventId).ExecuteAsync(cancellationToken);
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            // Already gone — the outcome we wanted, not a failure.
        }
    }

    public async Task<bool> EventExistsAsync(string eventId, CancellationToken cancellationToken = default)
    {
        var (service, calendarId) = RequireInitialized();
        try
        {
            await service.Events.Get(calendarId, eventId).ExecuteAsync(cancellationToken);
            return true;
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<Event>> ListUpcomingAsync(DateTime fromUtc, CancellationToken cancellationToken = default)
    {
        var (service, calendarId) = RequireInitialized();
        var events = new List<Event>();
        var request = service.Events.List(calendarId);
        request.TimeMinDateTimeOffset = new DateTimeOffset(DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc));
        request.SingleEvents = true;
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

        string? pageToken;
        do
        {
            var page = await request.ExecuteAsync(cancellationToken);
            events.AddRange(page.Items);
            pageToken = page.NextPageToken;
            request.PageToken = pageToken;
        } while (pageToken is not null);

        return events;
    }

    private (CalendarService Service, string CalendarId) RequireInitialized()
    {
        EnsureInitialized();
        if (_calendarService is null || _calendarId is null)
        {
            throw new InvalidOperationException(
                "GoogleCalendarClient is not configured — check IsConfigured before calling.");
        }

        return (_calendarService, _calendarId);
    }

    private void EnsureInitialized()
    {
        if (_initAttempted)
        {
            return;
        }

        lock (_initLock)
        {
            if (_initAttempted)
            {
                return;
            }

            _initAttempted = true;
            TryInitialize();
        }
    }

    private void TryInitialize()
    {
        var calendarId = _configuration["GoogleCalendar:CalendarId"];
        if (string.IsNullOrWhiteSpace(calendarId))
        {
            _logger.LogInformation("GoogleCalendar:CalendarId not configured — calendar sync disabled.");
            return;
        }

        try
        {
            var credential = LoadCredential();
            if (credential is null)
            {
                _logger.LogInformation(
                    "Neither GoogleCalendar:CredentialsJson nor GoogleCalendar:CredentialsPath is configured — calendar sync disabled.");
                return;
            }

            _calendarService = new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "Mobmek",
            });
            _calendarId = calendarId;

            _logger.LogInformation("Google Calendar sync initialized against calendar {CalendarId}.", calendarId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Google Calendar client — calendar sync disabled.");
            _calendarService = null;
            _calendarId = null;
        }
    }

    private GoogleCredential? LoadCredential()
    {
        var credentialsJson = _configuration["GoogleCalendar:CredentialsJson"];
        if (!string.IsNullOrWhiteSpace(credentialsJson))
        {
            return CredentialFactory.FromJson<ServiceAccountCredential>(credentialsJson)
                .ToGoogleCredential()
                .CreateScoped(CalendarService.Scope.Calendar);
        }

        var credentialsPath = _configuration["GoogleCalendar:CredentialsPath"];
        if (!string.IsNullOrWhiteSpace(credentialsPath) && File.Exists(credentialsPath))
        {
            var json = File.ReadAllText(credentialsPath);
            return CredentialFactory.FromJson<ServiceAccountCredential>(json)
                .ToGoogleCredential()
                .CreateScoped(CalendarService.Scope.Calendar);
        }

        return null;
    }
}
