using MobmekApi.Data;
using MobmekApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
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
    // with [AllowAnonymous] (only AuthController.Login does). Admin-only endpoints add
    // [Authorize(Roles = "Admin")], which layers a role check on top of this.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// --- Application services ---
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ILoginAttemptService, LoginAttemptService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IAccountAdminService, AccountAdminService>();
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
builder.Services.AddScoped<IJobItemService, JobItemService>();
builder.Services.AddScoped<ILabourService, LabourService>();
builder.Services.AddScoped<IJobServiceLineService, JobServiceLineService>();
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
builder.Services.AddScoped<IEmailComposeService, EmailComposeService>();
builder.Services.AddScoped<IOutboundEmailService, OutboundEmailService>();
builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>(client =>
{
    client.BaseAddress = new Uri("https://api.resend.com/");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHostedService<RecurringTransactionPostingJob>();
builder.Services.AddHostedService<OutboundStatusPollJob>();
builder.Services.AddHostedService<AccountPurgeJob>();

// Transaction receipts land on local disk for now; swap this registration for an
// S3-backed IFileStorage when file storage moves to the cloud.
builder.Services.AddSingleton<IFileStorage>(new LocalFileStorage(
    Path.Combine(builder.Environment.ContentRootPath, builder.Configuration["FileStorage:RootPath"] ?? "uploads")));

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Mobmek API v1");
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposed so the integration test host (WebApplicationFactory) can reference the entry point.
public partial class Program;
