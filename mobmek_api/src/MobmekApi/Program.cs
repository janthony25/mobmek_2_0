using System.Threading.RateLimiting;
using Amazon;
using Amazon.S3;
// Program.cs is top-level statements, so it compiles into the global namespace and doesn't
// pick up MobmekApi.* by enclosing-namespace lookup the way the controllers do.
using MobmekApi;
using MobmekApi.Data;
using MobmekApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
// Not `using MobmekApi.Entities` — MobmekApi.Entities.JobService clashes with
// MobmekApi.Services.JobService, both used by name below. ApplicationUser is qualified instead.
using ApplicationUser = MobmekApi.Entities.ApplicationUser;

// QuestPDF requires an explicit license acknowledgment before first use. Community is free
// for organizations under $1M USD annual revenue — fine at this business's scale.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// --- Database (PostgreSQL via EF Core) ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// AppDbContext.SaveChangesAsync reads the signed-in user off HttpContext to stamp
// BaseEntity.UpdatedByUserId/UpdatedByName — needs the accessor registered to be injected.
builder.Services.AddHttpContextAccessor();

// --- Identity (staff login) ---
// Auth cookies are signed/encrypted with the Data Protection key ring. Without a
// persisted, shared location it defaults to an ephemeral/per-instance store, which
// silently invalidates every signed-in session on restart or when scaled to >1
// instance. DataProtection:KeyPath is set to a mounted volume in docker-compose.yml;
// falls back to a folder under the content root for plain `dotnet run` locally.
builder.Services.AddDataProtection()
    .SetApplicationName("MobmekApi")
    .PersistKeysToFileSystem(new DirectoryInfo(
        builder.Configuration["DataProtection:KeyPath"]
            ?? Path.Combine(builder.Environment.ContentRootPath, "dataprotection-keys")));

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme);

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 10;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.User.RequireUniqueEmail = true;
        // Accounts created via role management start with EmailConfirmed = false and can't sign
        // in until the emailed confirmation code is used — enforced here via SignInManager's
        // built-in confirmed-email check rather than a hand-rolled one in AuthController.
        options.SignIn.RequireConfirmedEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "Mobmek.Auth";
    options.Cookie.HttpOnly = true;
    // "SameAsRequest" (not "Always") so login still works over plain HTTP in local/Docker
    // dev, which has no TLS today. Behind a TLS-terminating proxy in production this
    // resolves to Secure automatically, provided forwarded-proto headers are honored
    // (see the TLS item in docs/auth-module-design.md §6 Phase 2).
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
    options.SlidingExpiration = true;
    // This is an API, not a page app — on missing/denied auth, return a status code
    // instead of redirecting to a server-rendered login page that doesn't exist.
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization(options =>
{
    // Secure by default: every endpoint requires a signed-in user unless it opts out
    // with [AllowAnonymous] (only AuthController.Login does). Endpoints needing more than
    // "any signed-in user" add [Authorize(Policy = Permissions.X)], which layers one of the
    // permission checks below on top of this.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    // One policy per entry in the fixed Permissions catalog — satisfied by the matching claim
    // AppUserClaimsPrincipalFactory stamps onto the auth cookie at sign-in, which in turn comes
    // from whatever RolePermission grants exist for the user's role(s). Which roles hold which
    // permissions is admin-editable data, not code — this loop is the only place new code is
    // needed when a new permission is added to the catalog.
    foreach (var permission in Permissions.All)
    {
        options.AddPolicy(permission, policy =>
            policy.RequireClaim(AppUserClaimsPrincipalFactory.PermissionClaimType, permission));
    }
});

// --- Application services ---
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ILoginAttemptService, LoginAttemptService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IAccountAdminService, AccountAdminService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ICarMakeService, CarMakeService>();
builder.Services.AddScoped<ICarModelService, CarModelService>();
builder.Services.AddScoped<ICarService, CarService>();
builder.Services.AddScoped<IEmployeeTitleService, EmployeeTitleService>();
builder.Services.AddScoped<IEmploymentTypeService, EmploymentTypeService>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IJobServiceCatalogService, JobServiceCatalogService>();
builder.Services.AddScoped<IJobService, JobService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
// PublicBookingService takes its clock as a dependency rather than calling DateTime.UtcNow,
// so the slot grid can be tested against a fixed "now".
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IPublicBookingService, PublicBookingService>();
// Singleton: subscribers (open SSE connections) and the writers that notify them must share
// one instance across the whole app, not one per request.
builder.Services.AddSingleton<IAppointmentChangeNotifier, AppointmentChangeNotifier>();
builder.Services.AddScoped<IJobItemService, JobItemService>();
builder.Services.AddScoped<ILabourService, LabourService>();
builder.Services.AddScoped<IJobServiceLineService, JobServiceLineService>();
builder.Services.AddScoped<IJobPhotoService, JobPhotoService>();
builder.Services.AddScoped<IGstSettingService, GstSettingService>();
builder.Services.AddScoped<IGstReportService, GstReportService>();
builder.Services.AddScoped<IBusinessDetailsService, BusinessDetailsService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IInvoicePdfService, InvoicePdfService>();
builder.Services.AddScoped<IReminderTemplateService, ReminderTemplateService>();
builder.Services.AddScoped<INoteService, NoteService>();
builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.AddScoped<ICashAccountService, CashAccountService>();
builder.Services.AddScoped<ITransactionCategoryService, TransactionCategoryService>();
builder.Services.AddScoped<ICashTransactionService, CashTransactionService>();
builder.Services.AddScoped<ICashFlowSettingsService, CashFlowSettingsService>();
builder.Services.AddScoped<IRecurringTransactionService, RecurringTransactionService>();
builder.Services.AddScoped<IPlannedTransactionService, PlannedTransactionService>();
builder.Services.AddScoped<IForecastService, ForecastService>();
builder.Services.AddScoped<ICashFlowAuditService, CashFlowAuditService>();
builder.Services.AddScoped<IPayeeService, PayeeService>();
builder.Services.AddScoped<ICategorizationRuleService, CategorizationRuleService>();
builder.Services.AddScoped<IEmailSettingsService, EmailSettingsService>();
builder.Services.AddScoped<IEmailTemplateService, EmailTemplateService>();
builder.Services.AddScoped<IEmailComposeService, EmailComposeService>();
builder.Services.AddScoped<IOutboundEmailService, OutboundEmailService>();
builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>(client =>
{
    client.BaseAddress = new Uri("https://api.resend.com/");
    client.Timeout = TimeSpan.FromSeconds(15);
});
// Singleton: wraps one lazily-initialized Google CalendarService client, same lifetime as the
// legacy service it replaces. Cleanly disabled (IsConfigured = false) until GoogleCalendar:*
// config is present — nothing else in the app changes when it's not.
builder.Services.AddSingleton<IGoogleCalendarClient, GoogleCalendarClient>();
builder.Services.AddSingleton<CalendarSyncStatus>();
// Registered as its own singleton (not just via AddHostedService<T>) so CalendarSyncController
// can resolve the same running instance to trigger an on-demand reconcile.
builder.Services.AddSingleton<CalendarSyncJob>();
builder.Services.AddHostedService<RecurringTransactionPostingJob>();
builder.Services.AddHostedService<OutboundStatusPollJob>();
builder.Services.AddHostedService<AccountPurgeJob>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<CalendarSyncJob>());

// Uploaded files — job photos, transaction receipts, the business logo — all live behind
// IFileStorage. Local disk is the development default so nothing here needs AWS credentials;
// a real deployment sets FileStorage:Provider=S3, because a container's filesystem is thrown
// away on every rebuild while the database rows pointing into it survive, which would show up
// as a list of files that all fail to load.
var fileStorageProvider = builder.Configuration["FileStorage:Provider"] ?? "Local";
switch (fileStorageProvider.ToLowerInvariant())
{
    case "s3":
        var bucketName = builder.Configuration["FileStorage:S3:BucketName"];
        if (string.IsNullOrWhiteSpace(bucketName))
        {
            // Refuse to start rather than fail per-upload: without this the app comes up
            // looking healthy and every attachment dies on an opaque S3 error instead.
            throw new InvalidOperationException(
                "FileStorage:Provider is 'S3' but FileStorage:S3:BucketName is empty. Set it "
                + "(FILE_STORAGE_S3_BUCKET in .env) or switch the provider back to 'Local'.");
        }

        // No credentials are configured on purpose: the AWS SDK's default chain resolves the
        // EC2 instance role in production and a local profile in development, so no access
        // keys ever need to live in config. Region is resolved from the instance when unset.
        var region = builder.Configuration["FileStorage:S3:Region"];
        builder.Services.AddSingleton<IAmazonS3>(string.IsNullOrWhiteSpace(region)
            ? new AmazonS3Client()
            : new AmazonS3Client(RegionEndpoint.GetBySystemName(region)));
        builder.Services.AddSingleton<IFileStorage>(sp =>
            new S3FileStorage(sp.GetRequiredService<IAmazonS3>(), bucketName));
        break;

    case "local":
        builder.Services.AddSingleton<IFileStorage>(new LocalFileStorage(
            Path.Combine(builder.Environment.ContentRootPath, builder.Configuration["FileStorage:RootPath"] ?? "uploads")));
        break;

    default:
        // A typo must not silently degrade to local disk — in production that means uploads
        // quietly landing on a filesystem that the next deploy deletes.
        throw new InvalidOperationException(
            $"FileStorage:Provider '{fileStorageProvider}' is not recognised. Use 'Local' or 'S3'.");
}

// --- Public marketing site (anonymous booking endpoints) ---
// The booking page is served from a different origin than this API, so it needs an explicit
// CORS grant. Origins are configured, never wildcarded: these endpoints accept writes, and
// AllowAnyOrigin would let any site on the internet post into the workshop calendar.
var publicSiteOrigins = builder.Configuration
    .GetSection("PublicSite:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicies.PublicSite, policy =>
    {
        policy.WithMethods("GET", "POST").WithHeaders("Content-Type");

        if (builder.Environment.IsDevelopment())
        {
            // Any loopback port is fine locally, so the booking page works whether it's served
            // by VS Code Live Server, Vite, `python -m http.server` or anything else, without
            // needing a config edit and restart per port. Development only — production still
            // uses the explicit list below.
            policy.SetIsOriginAllowed(origin =>
                Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                && (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)));
        }
        else
        {
            policy.WithOrigins(publicSiteOrigins);
        }
    });
});

// Nothing authenticates the booking endpoints, so a per-IP rate limit is the only thing
// stopping a script from flooding the calendar with junk requests.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(RateLimitPolicies.PublicBookingRead, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                // Browsing the calendar week by week is cheap and idempotent — be generous
                // so a normal visitor clicking through dates is never throttled.
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
            }));

    options.AddPolicy(RateLimitPolicies.PublicBookingWrite, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                // A real person books once. Five an hour leaves room for retries and for a
                // household sharing an IP, while making bulk spam useless.
                PermitLimit = 5,
                Window = TimeSpan.FromHours(1),
            }));

    // Behind a reverse proxy every request arrives from the proxy's IP, which would collapse
    // all callers into one partition. Honour the forwarded header when present.
    static string ClientKey(HttpContext httpContext) =>
        httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
});

// --- MVC / API ---
builder.Services.AddControllers();
builder.Services.AddProblemDetails();

// --- Swagger / OpenAPI ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Mobmek API", Version = "v1" });
});

var app = builder.Build();

// In Development, apply any pending EF Core migrations on startup so the schema
// is ready without a manual `dotnet ef database update` step. Do NOT do this in
// production — run migrations as a deliberate, separate deployment step there.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    await CarReferenceDataSeeder.SeedAsync(db);
    await CashFlowSeeder.SeedAsync(db);
}

// Runs in every environment — production needs the bootstrap Admin too, not just dev.
// Assumes migrations have already been applied (auto in Development above; a deliberate
// deploy step in production).
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    await AdminSeeder.SeedAsync(
        services.GetRequiredService<AppDbContext>(),
        services.GetRequiredService<UserManager<ApplicationUser>>(),
        services.GetRequiredService<RoleManager<IdentityRole<Guid>>>(),
        services.GetRequiredService<IConfiguration>(),
        services.GetRequiredService<ILoggerFactory>().CreateLogger("AdminSeeder"));
}

// --- HTTP pipeline ---
app.UseExceptionHandler();

// nginx sets X-Forwarded-Proto/-For (mobmek_frontend/nginx.conf), but nothing translated them
// into Request.Scheme/RemoteIpAddress — so Kestrel always saw the proxy's internal plain-HTTP
// hop, never the browser's real HTTPS connection. Two concrete consequences without this:
// CookieSecurePolicy.SameAsRequest (below) would never add Secure to the auth cookie, and
// UseHttpsRedirection() would try to redirect every already-HTTPS request. Must run before both.
// KnownNetworks/KnownProxies are cleared (trust the immediate caller's header) rather than
// pinned to the nginx container's IP, which is dynamic across deploys — safe because the API's
// own port is never opened to the public internet (only 80/443 are, per the EC2 security group
// in infrastructure/docs/phase-1-plan.md); nothing outside the box can reach Kestrel directly
// to forge this header in the first place.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Mobmek API v1");
    });
}

// Ahead of UseHttpsRedirection on purpose: a CORS preflight that gets a 307 is treated as a
// failure by browsers (they don't follow redirects on OPTIONS), which would break the booking
// page whenever it calls the API over plain HTTP.
app.UseCors();

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposed so the integration test host (WebApplicationFactory) can reference the entry point.
public partial class Program;
