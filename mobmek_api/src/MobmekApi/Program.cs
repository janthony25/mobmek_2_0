using MobmekApi.Data;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Database (PostgreSQL via EF Core) ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Application services ---
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
builder.Services.AddScoped<IJobItemService, JobItemService>();
builder.Services.AddScoped<ILabourService, LabourService>();
builder.Services.AddScoped<IJobServiceLineService, JobServiceLineService>();
builder.Services.AddScoped<IGstSettingService, GstSettingService>();
builder.Services.AddScoped<IBusinessDetailsService, BusinessDetailsService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
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
builder.Services.AddHostedService<RecurringTransactionPostingJob>();

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
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposed so the integration test host (WebApplicationFactory) can reference the entry point.
public partial class Program;
