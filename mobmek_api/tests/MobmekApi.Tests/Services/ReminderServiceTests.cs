using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class ReminderServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static async Task<(Guid CustomerId, Guid CarId)> SeedCustomerWithCarAsync(AppDbContext db)
    {
        var customer = await new CustomerService(db).CreateAsync(
            new CreateCustomerRequest("Owner", "Person", "000", null, null, null));
        var make = await new CarMakeService(db).CreateAsync(new CreateCarMakeRequest("Toyota"));
        var model = await new CarModelService(db).CreateAsync(new CreateCarModelRequest(make.Id, "Hilux"));
        var (car, _) = await new CarService(db).CreateAsync(
            new CreateCarRequest(customer.Id, make.Id, model!.Id, 2020, "ABC123", null, null, null, null));
        return (customer.Id, car!.Id);
    }

    private static CreateReminderRequest NewReminder(
        Guid customerId, Guid? carId = null, Guid? templateId = null,
        string title = "Next Service", DateOnly? dueDate = null, bool isDone = false) =>
        new(customerId, carId, templateId, title, dueDate ?? new DateOnly(2026, 9, 1), isDone, null);

    [Fact]
    public async Task CreateAsync_PersistsReminder_ForCustomerOnly()
    {
        await using var db = CreateContext();
        var (customerId, _) = await SeedCustomerWithCarAsync(db);
        var service = new ReminderService(db);

        var (reminder, error) = await service.CreateAsync(NewReminder(customerId));

        Assert.Equal(ReminderWriteError.None, error);
        Assert.NotNull(reminder);
        Assert.Equal("Owner Person", reminder!.CustomerName);
        Assert.Null(reminder.CarId);
    }

    [Fact]
    public async Task CreateAsync_ResolvesCarLabelAndTemplate_WhenProvided()
    {
        await using var db = CreateContext();
        var (customerId, carId) = await SeedCustomerWithCarAsync(db);
        var template = await new ReminderTemplateService(db).CreateAsync(
            new CreateReminderTemplateRequest("Next WOF", null, 12));
        var service = new ReminderService(db);

        var (reminder, error) = await service.CreateAsync(
            NewReminder(customerId, carId, template.Id, title: "Next WOF"));

        Assert.Equal(ReminderWriteError.None, error);
        Assert.Equal(carId, reminder!.CarId);
        Assert.Contains("Hilux", reminder.CarLabel);
        Assert.Equal("Next WOF", reminder.ReminderTemplateName);
    }

    [Fact]
    public async Task CreateAsync_ReturnsCustomerNotFound_WhenCustomerMissing()
    {
        await using var db = CreateContext();
        var service = new ReminderService(db);

        var (reminder, error) = await service.CreateAsync(NewReminder(Guid.NewGuid()));

        Assert.Null(reminder);
        Assert.Equal(ReminderWriteError.CustomerNotFound, error);
    }

    [Fact]
    public async Task CreateAsync_ReturnsCarNotOwnedByCustomer_WhenCarBelongsToAnother()
    {
        await using var db = CreateContext();
        var (customerId, _) = await SeedCustomerWithCarAsync(db);
        var (_, otherCarId) = await SeedCustomerWithCarAsync(db);
        var service = new ReminderService(db);

        var (reminder, error) = await service.CreateAsync(NewReminder(customerId, otherCarId));

        Assert.Null(reminder);
        Assert.Equal(ReminderWriteError.CarNotOwnedByCustomer, error);
    }

    [Fact]
    public async Task CreateAsync_ReturnsTemplateNotFound_WhenTemplateMissing()
    {
        await using var db = CreateContext();
        var (customerId, _) = await SeedCustomerWithCarAsync(db);
        var service = new ReminderService(db);

        var (reminder, error) = await service.CreateAsync(
            NewReminder(customerId, templateId: Guid.NewGuid()));

        Assert.Null(reminder);
        Assert.Equal(ReminderWriteError.TemplateNotFound, error);
    }

    [Fact]
    public async Task GetAllAsync_OrdersOutstandingBySoonestDue_ThenDoneLast()
    {
        await using var db = CreateContext();
        var (customerId, _) = await SeedCustomerWithCarAsync(db);
        var service = new ReminderService(db);
        await service.CreateAsync(NewReminder(customerId, title: "Later", dueDate: new DateOnly(2026, 12, 1)));
        await service.CreateAsync(NewReminder(customerId, title: "Sooner", dueDate: new DateOnly(2026, 8, 1)));
        await service.CreateAsync(NewReminder(customerId, title: "Done", dueDate: new DateOnly(2026, 1, 1), isDone: true));

        var result = await service.GetAllAsync(customerId);

        Assert.Collection(result,
            r => Assert.Equal("Sooner", r.Title),
            r => Assert.Equal("Later", r.Title),
            r => Assert.Equal("Done", r.Title));
    }

    [Fact]
    public async Task GetAllAsync_ExcludesDone_WhenIncludeDoneFalse()
    {
        await using var db = CreateContext();
        var (customerId, _) = await SeedCustomerWithCarAsync(db);
        var service = new ReminderService(db);
        await service.CreateAsync(NewReminder(customerId, title: "Open"));
        await service.CreateAsync(NewReminder(customerId, title: "Done", isDone: true));

        var result = await service.GetAllAsync(customerId, includeDone: false);

        Assert.Single(result);
        Assert.Equal("Open", result[0].Title);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByCar()
    {
        await using var db = CreateContext();
        var (customerId, carId) = await SeedCustomerWithCarAsync(db);
        var service = new ReminderService(db);
        await service.CreateAsync(NewReminder(customerId, carId, title: "Car reminder"));
        await service.CreateAsync(NewReminder(customerId, title: "Customer reminder"));

        var result = await service.GetAllAsync(customerId, carId);

        Assert.Single(result);
        Assert.Equal("Car reminder", result[0].Title);
    }

    [Fact]
    public async Task UpdateAsync_ModifiesFields_AndStampsUpdatedAt()
    {
        await using var db = CreateContext();
        var (customerId, carId) = await SeedCustomerWithCarAsync(db);
        var service = new ReminderService(db);
        var (created, _) = await service.CreateAsync(NewReminder(customerId));

        var (updated, error) = await service.UpdateAsync(
            created!.Id, new UpdateReminderRequest(carId, null, "Updated", new DateOnly(2027, 1, 1), true, "note"));

        Assert.Equal(ReminderWriteError.None, error);
        Assert.Equal("Updated", updated!.Title);
        Assert.Equal(carId, updated.CarId);
        Assert.True(updated.IsDone);
        Assert.NotNull(updated.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFound_WhenMissing()
    {
        await using var db = CreateContext();
        var service = new ReminderService(db);

        var (reminder, error) = await service.UpdateAsync(
            Guid.NewGuid(), new UpdateReminderRequest(null, null, "X", new DateOnly(2026, 9, 1), false, null));

        Assert.Null(reminder);
        Assert.Equal(ReminderWriteError.NotFound, error);
    }

    [Fact]
    public async Task DeleteAsync_RemovesReminder_AndReturnsTrue()
    {
        await using var db = CreateContext();
        var (customerId, _) = await SeedCustomerWithCarAsync(db);
        var service = new ReminderService(db);
        var (created, _) = await service.CreateAsync(NewReminder(customerId));

        Assert.True(await service.DeleteAsync(created!.Id));
        Assert.Equal(0, await db.Reminders.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenMissing()
    {
        await using var db = CreateContext();
        var service = new ReminderService(db);

        Assert.False(await service.DeleteAsync(Guid.NewGuid()));
    }
}
