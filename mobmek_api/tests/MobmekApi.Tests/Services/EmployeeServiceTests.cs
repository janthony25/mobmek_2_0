using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class EmployeeServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static async Task<(Guid TitleId, Guid TypeId)> SeedLookupsAsync(AppDbContext db)
    {
        var title = await new EmployeeTitleService(db).CreateAsync(new CreateEmployeeTitleRequest("Mechanic"));
        var type = await new EmploymentTypeService(db).CreateAsync(new CreateEmploymentTypeRequest("Full-time"));
        return (title.Id, type.Id);
    }

    private static CreateEmployeeRequest NewEmployee(Guid titleId, Guid typeId, string first = "Jane", string last = "Doe") =>
        new(first, last, titleId, typeId, "+1-555-0100", "jane@example.com", "1 Main St");

    [Fact]
    public async Task CreateAsync_PersistsEmployee_AndResolvesTitleAndTypeNames()
    {
        await using var db = CreateContext();
        var (titleId, typeId) = await SeedLookupsAsync(db);
        var service = new EmployeeService(db);

        var (employee, error) = await service.CreateAsync(NewEmployee(titleId, typeId));

        Assert.Equal(EmployeeWriteError.None, error);
        Assert.NotNull(employee);
        Assert.NotEqual(Guid.Empty, employee!.Id);
        Assert.Equal("Mechanic", employee.TitleName);
        Assert.Equal("Full-time", employee.EmploymentTypeName);
        Assert.Equal(1, await db.Employees.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_ReturnsTitleNotFound_WhenTitleMissing()
    {
        await using var db = CreateContext();
        var (_, typeId) = await SeedLookupsAsync(db);
        var service = new EmployeeService(db);

        var (employee, error) = await service.CreateAsync(NewEmployee(Guid.NewGuid(), typeId));

        Assert.Null(employee);
        Assert.Equal(EmployeeWriteError.TitleNotFound, error);
        Assert.Equal(0, await db.Employees.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_ReturnsEmploymentTypeNotFound_WhenTypeMissing()
    {
        await using var db = CreateContext();
        var (titleId, _) = await SeedLookupsAsync(db);
        var service = new EmployeeService(db);

        var (employee, error) = await service.CreateAsync(NewEmployee(titleId, Guid.NewGuid()));

        Assert.Null(employee);
        Assert.Equal(EmployeeWriteError.EmploymentTypeNotFound, error);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmployeesOrderedByLastThenFirstName()
    {
        await using var db = CreateContext();
        var (titleId, typeId) = await SeedLookupsAsync(db);
        var service = new EmployeeService(db);
        await service.CreateAsync(NewEmployee(titleId, typeId, "Bob", "Zephyr"));
        await service.CreateAsync(NewEmployee(titleId, typeId, "Carol", "Adams"));
        await service.CreateAsync(NewEmployee(titleId, typeId, "Alice", "Adams"));

        var result = await service.GetAllAsync();

        Assert.Collection(result,
            e => Assert.Equal(("Adams", "Alice"), (e.LastName, e.FirstName)),
            e => Assert.Equal(("Adams", "Carol"), (e.LastName, e.FirstName)),
            e => Assert.Equal(("Zephyr", "Bob"), (e.LastName, e.FirstName)));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenMissing()
    {
        await using var db = CreateContext();
        var service = new EmployeeService(db);

        Assert.Null(await service.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateAsync_ModifiesFields_AndStampsUpdatedAt()
    {
        await using var db = CreateContext();
        var (titleId, typeId) = await SeedLookupsAsync(db);
        var service = new EmployeeService(db);
        var (created, _) = await service.CreateAsync(NewEmployee(titleId, typeId, "Old", "Name"));

        var (updated, error) = await service.UpdateAsync(created!.Id,
            new UpdateEmployeeRequest("New", "Name", titleId, typeId, "999", "new@example.com", "2 Other St"));

        Assert.Equal(EmployeeWriteError.None, error);
        Assert.NotNull(updated);
        Assert.Equal("New", updated!.FirstName);
        Assert.Equal("new@example.com", updated.EmailAddress);
        Assert.NotNull(updated.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFound_WhenMissing()
    {
        await using var db = CreateContext();
        var (titleId, typeId) = await SeedLookupsAsync(db);
        var service = new EmployeeService(db);

        var (employee, error) = await service.UpdateAsync(Guid.NewGuid(),
            new UpdateEmployeeRequest("X", "Y", titleId, typeId, "0", "x@example.com", "addr"));

        Assert.Null(employee);
        Assert.Equal(EmployeeWriteError.NotFound, error);
    }

    [Fact]
    public async Task DeleteAsync_RemovesEmployee_AndReturnsTrue()
    {
        await using var db = CreateContext();
        var (titleId, typeId) = await SeedLookupsAsync(db);
        var service = new EmployeeService(db);
        var (created, _) = await service.CreateAsync(NewEmployee(titleId, typeId, "Temp", "Worker"));

        Assert.True(await service.DeleteAsync(created!.Id));
        Assert.Equal(0, await db.Employees.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenMissing()
    {
        await using var db = CreateContext();
        var service = new EmployeeService(db);

        Assert.False(await service.DeleteAsync(Guid.NewGuid()));
    }
}
