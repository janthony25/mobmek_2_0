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
builder.Services.AddScoped<ICarService, CarService>();
builder.Services.AddScoped<IEmployeeTitleService, EmployeeTitleService>();
builder.Services.AddScoped<IEmploymentTypeService, EmploymentTypeService>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();

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
