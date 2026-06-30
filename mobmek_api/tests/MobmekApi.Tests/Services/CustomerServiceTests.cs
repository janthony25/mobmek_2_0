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
}
