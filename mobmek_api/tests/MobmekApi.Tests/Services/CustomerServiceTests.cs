using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class CustomerServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task CreateAsync_PersistsCustomer_AndReturnsDto()
    {
        await using var db = CreateContext();
        var service = new CustomerService(db);

        var result = await service.CreateAsync(
            new CreateCustomerRequest("Ada", "Lovelace", "+1-555-0100", "ada@example.com", "1 Analytical Way", "VIP"));

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Ada", result.FirstName);
        Assert.Equal("ada@example.com", result.EmailAddress);
        Assert.Equal(1, await db.Customers.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_AllowsNullOptionalFields()
    {
        await using var db = CreateContext();
        var service = new CustomerService(db);

        var result = await service.CreateAsync(
            new CreateCustomerRequest("Grace", "Hopper", "+1-555-0199", null, null, null));

        Assert.Null(result.EmailAddress);
        Assert.Null(result.PhysicalAddress);
        Assert.Null(result.Notes);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenMissing()
    {
        await using var db = CreateContext();
        var service = new CustomerService(db);

        var result = await service.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsCustomersOrderedByLastThenFirstName()
    {
        await using var db = CreateContext();
        var service = new CustomerService(db);
        await service.CreateAsync(new CreateCustomerRequest("Bob", "Zephyr", "1", null, null, null));
        await service.CreateAsync(new CreateCustomerRequest("Carol", "Adams", "2", null, null, null));
        await service.CreateAsync(new CreateCustomerRequest("Alice", "Adams", "3", null, null, null));

        var result = await service.GetAllAsync();

        Assert.Collection(result,
            c => Assert.Equal(("Adams", "Alice"), (c.LastName, c.FirstName)),
            c => Assert.Equal(("Adams", "Carol"), (c.LastName, c.FirstName)),
            c => Assert.Equal(("Zephyr", "Bob"), (c.LastName, c.FirstName)));
    }

    [Fact]
    public async Task UpdateAsync_ModifiesFields_AndStampsUpdatedAt()
    {
        await using var db = CreateContext();
        var service = new CustomerService(db);
        var created = await service.CreateAsync(
            new CreateCustomerRequest("Old", "Name", "000", "old@example.com", null, null));

        var updated = await service.UpdateAsync(
            created.Id, new UpdateCustomerRequest("New", "Name", "111", null, "New Address", "noted"));

        Assert.NotNull(updated);
        Assert.Equal("New", updated!.FirstName);
        Assert.Equal("111", updated.PhoneNumber);
        Assert.Null(updated.EmailAddress);
        Assert.Equal("New Address", updated.PhysicalAddress);
        Assert.NotNull(updated.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenMissing()
    {
        await using var db = CreateContext();
        var service = new CustomerService(db);

        var result = await service.UpdateAsync(
            Guid.NewGuid(), new UpdateCustomerRequest("X", "Y", "0", null, null, null));

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_RemovesCustomer_AndReturnsTrue()
    {
        await using var db = CreateContext();
        var service = new CustomerService(db);
        var created = await service.CreateAsync(
            new CreateCustomerRequest("Temp", "Customer", "0", null, null, null));

        var deleted = await service.DeleteAsync(created.Id);

        Assert.True(deleted);
        Assert.Equal(0, await db.Customers.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenMissing()
    {
        await using var db = CreateContext();
        var service = new CustomerService(db);

        var deleted = await service.DeleteAsync(Guid.NewGuid());

        Assert.False(deleted);
    }

    [Fact]
    public async Task GetPagedAsync_OrdersNewestFirst_AndSlices()
    {
        await using var db = CreateContext();
        var service = new CustomerService(db);
        var lastNames = new[] { "Adams", "Baker", "Clark", "Davis", "Evans" };
        for (var i = 0; i < lastNames.Length; i++)
        {
            var created = await service.CreateAsync(
                new CreateCustomerRequest("Ann", lastNames[i], "0", null, null, null));
            var entity = await db.Customers.FirstAsync(c => c.Id == created.Id);
            entity.CreatedAtUtc = new DateTime(2026, 1, 1 + i, 0, 0, 0, DateTimeKind.Utc);
        }
        await db.SaveChangesAsync();

        var page2 = await service.GetPagedAsync(page: 2, pageSize: 2, search: null);

        // Newest first: Evans, Davis | Clark, Baker | Adams.
        Assert.Equal(5, page2.TotalCount);
        Assert.Equal(2, page2.Page);
        Assert.Equal(2, page2.PageSize);
        Assert.Collection(page2.Items,
            c => Assert.Equal("Clark", c.LastName),
            c => Assert.Equal("Baker", c.LastName));
    }

    [Fact]
    public async Task GetPagedAsync_SortByOldest_ReversesTheDefaultNewestFirstOrder()
    {
        await using var db = CreateContext();
        var service = new CustomerService(db);
        var lastNames = new[] { "Adams", "Baker", "Clark" };
        for (var i = 0; i < lastNames.Length; i++)
        {
            var created = await service.CreateAsync(new CreateCustomerRequest("Ann", lastNames[i], "0", null, null, null));
            (await db.Customers.FirstAsync(c => c.Id == created.Id)).CreatedAtUtc = new DateTime(2026, 1, 1 + i, 0, 0, 0, DateTimeKind.Utc);
        }
        await db.SaveChangesAsync();

        var oldest = await service.GetPagedAsync(1, 10, null, sortBy: "oldest");

        Assert.Collection(oldest.Items,
            c => Assert.Equal("Adams", c.LastName),
            c => Assert.Equal("Baker", c.LastName),
            c => Assert.Equal("Clark", c.LastName));
    }

    [Fact]
    public async Task GetPagedAsync_SortByName_OrdersByLastThenFirstName()
    {
        await using var db = CreateContext();
        var service = new CustomerService(db);
        await service.CreateAsync(new CreateCustomerRequest("Zed", "Adams", "0", null, null, null));
        await service.CreateAsync(new CreateCustomerRequest("Ann", "Baker", "0", null, null, null));

        var byName = await service.GetPagedAsync(1, 10, null, sortBy: "name");

        Assert.Collection(byName.Items,
            c => Assert.Equal("Adams", c.LastName),
            c => Assert.Equal("Baker", c.LastName));
    }

    [Fact]
    public async Task GetPagedAsync_DateRangeFiltersByCreatedDate()
    {
        await using var db = CreateContext();
        var service = new CustomerService(db);
        var lastNames = new[] { "Adams", "Baker", "Clark" };
        for (var i = 0; i < lastNames.Length; i++)
        {
            var created = await service.CreateAsync(new CreateCustomerRequest("Ann", lastNames[i], "0", null, null, null));
            (await db.Customers.FirstAsync(c => c.Id == created.Id)).CreatedAtUtc = new DateTime(2026, 1, 1 + i, 0, 0, 0, DateTimeKind.Utc);
        }
        await db.SaveChangesAsync();

        var middleDayOnly = await service.GetPagedAsync(
            1, 10, null, dateFrom: new DateOnly(2026, 1, 2), dateTo: new DateOnly(2026, 1, 2));

        var item = Assert.Single(middleDayOnly.Items);
        Assert.Equal("Baker", item.LastName);
    }

    [Fact]
    public async Task GetPagedAsync_PageBeyondEnd_ReturnsEmptyWithTotal()
    {
        await using var db = CreateContext();
        var service = new CustomerService(db);
        await service.CreateAsync(new CreateCustomerRequest("Solo", "Customer", "0", null, null, null));

        var result = await service.GetPagedAsync(page: 3, pageSize: 10, search: null);

        Assert.Empty(result.Items);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_SearchMatchesNamePhoneAndEmail_CaseInsensitively()
    {
        await using var db = CreateContext();
        var service = new CustomerService(db);
        await service.CreateAsync(new CreateCustomerRequest("Ada", "Lovelace", "021-555-777", "ada@example.com", null, null));
        await service.CreateAsync(new CreateCustomerRequest("Grace", "Hopper", "09-1234", null, null, null));

        Assert.Single((await service.GetPagedAsync(1, 10, "LOVELACE")).Items);
        Assert.Single((await service.GetPagedAsync(1, 10, "ada love")).Items); // across full name
        Assert.Single((await service.GetPagedAsync(1, 10, "021-555")).Items);
        Assert.Single((await service.GetPagedAsync(1, 10, "ada@example")).Items);
        Assert.Equal(2, (await service.GetPagedAsync(1, 10, "  ")).TotalCount); // blank = no filter
        Assert.Empty((await service.GetPagedAsync(1, 10, "nobody")).Items);
    }

    [Fact]
    public async Task GetPagedAsync_SearchMatchesRego_CaseInsensitively()
    {
        await using var db = CreateContext();
        var service = new CustomerService(db);
        var owner = await service.CreateAsync(new CreateCustomerRequest("Ada", "Lovelace", "0", null, null, null));
        await service.CreateAsync(new CreateCustomerRequest("Grace", "Hopper", "1", null, null, null));

        var make = new Entities.CarMake { Name = "Toyota" };
        var model = new Entities.CarModel { Name = "Hilux", CarMake = make };
        db.Cars.Add(new Entities.Car { CustomerId = owner.Id, CarMake = make, CarModel = model, Year = 2020, Rego = "ABC123" });
        await db.SaveChangesAsync();

        var result = await service.GetPagedAsync(1, 10, "abc123");

        var item = Assert.Single(result.Items);
        Assert.Equal(owner.Id, item.Id);
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsCardAggregates_ExcludingDoneItems()
    {
        await using var db = CreateContext();
        var service = new CustomerService(db);
        var customer = await service.CreateAsync(new CreateCustomerRequest("Ada", "Lovelace", "0", null, null, null));

        var make = new Entities.CarMake { Name = "Toyota" };
        var model = new Entities.CarModel { Name = "Hilux", CarMake = make };
        var car = new Entities.Car { CustomerId = customer.Id, CarMake = make, CarModel = model, Year = 2020, Rego = "ABC123" };
        db.Cars.Add(car);
        db.Reminders.Add(new Entities.Reminder { CustomerId = customer.Id, CarId = car.Id, Title = "WOF", DueDate = new DateOnly(2026, 8, 1) });
        db.Reminders.Add(new Entities.Reminder { CustomerId = customer.Id, CarId = car.Id, Title = "Service", DueDate = new DateOnly(2026, 7, 10) });
        db.Reminders.Add(new Entities.Reminder { CustomerId = customer.Id, CarId = car.Id, Title = "Done", DueDate = new DateOnly(2026, 1, 1), IsDone = true });
        db.Notes.Add(new Entities.Note { CustomerId = customer.Id, Title = "Call back", DueDate = new DateOnly(2026, 7, 20) });
        db.Notes.Add(new Entities.Note { CustomerId = customer.Id, Title = "Done note", IsDone = true });
        await db.SaveChangesAsync();

        var result = await service.GetPagedAsync(1, 10, null);

        var item = Assert.Single(result.Items);
        var carSummary = Assert.Single(item.Cars);
        Assert.Equal("Toyota", carSummary.CarMakeName);
        Assert.Equal("Hilux", carSummary.CarModelName);
        Assert.Equal(2, carSummary.ActiveReminderCount);
        Assert.Equal(new DateOnly(2026, 7, 10), carSummary.NextReminderDueDate); // earliest active
        Assert.Equal(1, item.ActiveNoteCount);
        Assert.Equal(new DateOnly(2026, 7, 20), item.NextNoteDueDate);
    }
}
