using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class LoginAttemptServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static async Task<Guid> SeedEmployeeAsync(AppDbContext db)
    {
        var title = await new EmployeeTitleService(db).CreateAsync(new CreateEmployeeTitleRequest("Mechanic"));
        var type = await new EmploymentTypeService(db).CreateAsync(new CreateEmploymentTypeRequest("Full-time"));
        var (employee, _) = await new EmployeeService(db).CreateAsync(
            new CreateEmployeeRequest("Jane", "Doe", title.Id, type.Id, "+1-555-0100", "jane@example.com", "1 Main St"));
        return employee!.Id;
    }

    [Fact]
    public async Task RecordAsync_PersistsAttempt()
    {
        await using var db = CreateContext();
        var employeeId = await SeedEmployeeAsync(db);
        var service = new LoginAttemptService(db);

        await service.RecordAsync("jane@example.com", employeeId, succeeded: true, failureReason: null, ipAddress: "127.0.0.1");

        Assert.Equal(1, await db.LoginAttempts.CountAsync());
        var attempt = await db.LoginAttempts.SingleAsync();
        Assert.True(attempt.Succeeded);
        Assert.Equal(employeeId, attempt.EmployeeId);
        Assert.Null(attempt.FailureReason);
    }

    [Fact]
    public async Task RecordAsync_PersistsFailedAttempt_WithNullEmployeeId_WhenEmailUnknown()
    {
        await using var db = CreateContext();
        var service = new LoginAttemptService(db);

        await service.RecordAsync("nobody@example.com", employeeId: null, succeeded: false, failureReason: "InvalidCredentials", ipAddress: "10.0.0.1");

        var attempt = await db.LoginAttempts.SingleAsync();
        Assert.False(attempt.Succeeded);
        Assert.Null(attempt.EmployeeId);
        Assert.Equal("InvalidCredentials", attempt.FailureReason);
    }

    [Fact]
    public async Task GetPagedAsync_ResolvesEmployeeName_AndReturnsBothAttempts()
    {
        await using var db = CreateContext();
        var employeeId = await SeedEmployeeAsync(db);
        var service = new LoginAttemptService(db);

        await service.RecordAsync("jane@example.com", employeeId, succeeded: true, failureReason: null, ipAddress: "127.0.0.1");
        await service.RecordAsync("jane@example.com", employeeId, succeeded: false, failureReason: "LockedOut", ipAddress: "127.0.0.1");

        var page = await service.GetPagedAsync();

        Assert.Equal(2, page.TotalCount);
        Assert.Contains(page.Items, a => a.Succeeded && a.FailureReason == null);
        Assert.Contains(page.Items, a => !a.Succeeded && a.FailureReason == "LockedOut");
        Assert.All(page.Items, a => Assert.Equal("Jane Doe", a.EmployeeName));
    }

    [Fact]
    public async Task GetPagedAsync_ClampsPageSize()
    {
        await using var db = CreateContext();
        var service = new LoginAttemptService(db);
        await service.RecordAsync("a@example.com", null, true, null, null);

        var page = await service.GetPagedAsync(page: 0, pageSize: 1000);

        Assert.Equal(1, page.Page);
        Assert.Equal(200, page.PageSize);
    }
}
