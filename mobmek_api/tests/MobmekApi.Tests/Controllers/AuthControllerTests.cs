using MobmekApi.Controllers;
using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using MobmekApi.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MobmekApi.Tests.Controllers;

/// <summary>
/// Exercises AuthController.Login end to end against the real Identity stack (UserManager +
/// SignInManager, wired the same way as Program.cs) rather than mocking SignInManager — the
/// branching between "email not confirmed" / "deactivated" / "generic lockout" / "bad
/// credentials" / success previously had zero coverage above the service layer.
/// </summary>
public class AuthControllerTests
{
    private static (AppDbContext Db, UserManager<ApplicationUser> UserManager, SignInManager<ApplicationUser> SignInManager, HttpContext HttpContext)
        CreateContext()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var httpContext = new DefaultHttpContext();

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddLogging();
        services.AddDataProtection();
        // ProblemDetailsFactory, used by ControllerBase.Problem(...), is normally registered by
        // AddControllers() in Program.cs — add just the minimal MVC core slice here.
        services.AddMvcCore();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = httpContext });
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddCookie(IdentityConstants.ApplicationScheme);
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        var provider = services.BuildServiceProvider();
        // Assigned after the container is built — SignInManager only reads HttpContext lazily
        // when a method is called, by which point this is populated.
        httpContext.RequestServices = provider;

        return (
            db,
            provider.GetRequiredService<UserManager<ApplicationUser>>(),
            provider.GetRequiredService<SignInManager<ApplicationUser>>(),
            httpContext);
    }

    private static async Task<(Employee Employee, ApplicationUser User)> SeedUserAsync(
        AppDbContext db, UserManager<ApplicationUser> userManager,
        string email = "mechanic@example.com", string password = "Passw0rd!123",
        bool emailConfirmed = true)
    {
        var title = await new EmployeeTitleService(db).CreateAsync(new CreateEmployeeTitleRequest("Mechanic"));
        var type = await new EmploymentTypeService(db).CreateAsync(new CreateEmploymentTypeRequest("Full-time"));
        var (employeeDto, _) = await new EmployeeService(db).CreateAsync(
            new CreateEmployeeRequest("Jane", "Doe", title.Id, type.Id, "0211234567", email, "1 Main St"));
        var employee = (await db.Employees.FindAsync(employeeDto!.Id))!;

        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = emailConfirmed, EmployeeId = employee.Id };
        var result = await userManager.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(e => e.Description)));

        return (employee, user);
    }

    private static AuthController BuildController(
        AppDbContext db, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, HttpContext httpContext) =>
        new(
            signInManager, userManager, new AuthService(db, userManager), new LoginAttemptService(db),
            // Login doesn't touch IAccountService — not under test here, see AccountServiceTests.cs
            // for the forgot-password flow's own coverage.
            new AccountService(db, userManager, new FakeEmailSender(), new EmailSettingsService(db, new ConfigurationBuilder().Build())))
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };

    [Fact]
    public async Task Login_Succeeds_ForConfirmedUserWithCorrectPassword()
    {
        var (db, userManager, signInManager, httpContext) = CreateContext();
        await SeedUserAsync(db, userManager, "jane@example.com", "Passw0rd!123");
        var controller = BuildController(db, userManager, signInManager, httpContext);

        var result = await controller.Login(new LoginRequest("jane@example.com", "Passw0rd!123"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<CurrentUserDto>(ok.Value);
        Assert.Equal("jane@example.com", dto.Email);
        Assert.Equal(1, await db.LoginAttempts.CountAsync(a => a.Succeeded));
    }

    [Fact]
    public async Task Login_ReturnsNotAllowed_WhenEmailNotConfirmed()
    {
        var (db, userManager, signInManager, httpContext) = CreateContext();
        await SeedUserAsync(db, userManager, "pending@example.com", "Passw0rd!123", emailConfirmed: false);
        var controller = BuildController(db, userManager, signInManager, httpContext);

        var result = await controller.Login(new LoginRequest("pending@example.com", "Passw0rd!123"), CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
        var detail = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Contains("activated", detail.Detail);
        Assert.Equal(1, await db.LoginAttempts.CountAsync(a => !a.Succeeded && a.FailureReason == "EmailNotConfirmed"));
    }

    [Fact]
    public async Task Login_ReturnsDeactivatedMessage_WhenAccountDeactivated()
    {
        var (db, userManager, signInManager, httpContext) = CreateContext();
        var (_, user) = await SeedUserAsync(db, userManager, "exstaff@example.com", "Passw0rd!123");
        user.DeactivatedAtUtc = DateTime.UtcNow;
        await userManager.UpdateAsync(user);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        var controller = BuildController(db, userManager, signInManager, httpContext);

        var result = await controller.Login(new LoginRequest("exstaff@example.com", "Passw0rd!123"), CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
        var detail = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Contains("deactivated", detail.Detail);
        Assert.Equal(1, await db.LoginAttempts.CountAsync(a => !a.Succeeded && a.FailureReason == "LockedOut"));
    }

    [Fact]
    public async Task Login_ReturnsGenericUnauthorized_WhenLockedOutButNotDeactivated()
    {
        var (db, userManager, signInManager, httpContext) = CreateContext();
        var (_, user) = await SeedUserAsync(db, userManager, "lockedout@example.com", "Passw0rd!123");
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(30));
        var controller = BuildController(db, userManager, signInManager, httpContext);

        var result = await controller.Login(new LoginRequest("lockedout@example.com", "Passw0rd!123"), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task Login_ReturnsGenericUnauthorized_ForWrongPassword()
    {
        var (db, userManager, signInManager, httpContext) = CreateContext();
        await SeedUserAsync(db, userManager, "jane2@example.com", "Passw0rd!123");
        var controller = BuildController(db, userManager, signInManager, httpContext);

        var result = await controller.Login(new LoginRequest("jane2@example.com", "WrongPassword!1"), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
        Assert.Equal(1, await db.LoginAttempts.CountAsync(a => !a.Succeeded && a.FailureReason == "InvalidCredentials"));
    }

    [Fact]
    public async Task Login_ReturnsGenericUnauthorized_ForUnknownEmail_WithoutLeakingExistence()
    {
        var (db, userManager, signInManager, httpContext) = CreateContext();
        var controller = BuildController(db, userManager, signInManager, httpContext);

        var result = await controller.Login(new LoginRequest("nobody@example.com", "Whatever!123"), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }
}
