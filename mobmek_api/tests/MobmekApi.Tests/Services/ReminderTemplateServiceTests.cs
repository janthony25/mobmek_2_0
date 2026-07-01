using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class ReminderTemplateServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task CreateAsync_PersistsTemplate_AndReturnsDto()
    {
        await using var db = CreateContext();
        var service = new ReminderTemplateService(db);

        var result = await service.CreateAsync(new CreateReminderTemplateRequest("Next WOF", "Warrant of fitness", 12));

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Next WOF", result.Name);
        Assert.Equal(12, result.DefaultIntervalMonths);
        Assert.Equal(1, await db.ReminderTemplates.CountAsync());
    }

    [Fact]
    public async Task GetAllAsync_ReturnsTemplatesOrderedByName()
    {
        await using var db = CreateContext();
        var service = new ReminderTemplateService(db);
        await service.CreateAsync(new CreateReminderTemplateRequest("Next WOF", null, 12));
        await service.CreateAsync(new CreateReminderTemplateRequest("Next Service", null, 6));

        var result = await service.GetAllAsync();

        Assert.Collection(result,
            t => Assert.Equal("Next Service", t.Name),
            t => Assert.Equal("Next WOF", t.Name));
    }

    [Fact]
    public async Task UpdateAsync_ModifiesFields_AndStampsUpdatedAt()
    {
        await using var db = CreateContext();
        var service = new ReminderTemplateService(db);
        var created = await service.CreateAsync(new CreateReminderTemplateRequest("Old", null, null));

        var updated = await service.UpdateAsync(created.Id, new UpdateReminderTemplateRequest("New", "desc", 24));

        Assert.NotNull(updated);
        Assert.Equal("New", updated!.Name);
        Assert.Equal(24, updated.DefaultIntervalMonths);
        Assert.NotNull(updated.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenMissing()
    {
        await using var db = CreateContext();
        var service = new ReminderTemplateService(db);

        Assert.Null(await service.UpdateAsync(Guid.NewGuid(), new UpdateReminderTemplateRequest("X", null, null)));
    }

    [Fact]
    public async Task DeleteAsync_RemovesTemplate_AndReturnsTrue()
    {
        await using var db = CreateContext();
        var service = new ReminderTemplateService(db);
        var created = await service.CreateAsync(new CreateReminderTemplateRequest("Temp", null, null));

        Assert.True(await service.DeleteAsync(created.Id));
        Assert.Equal(0, await db.ReminderTemplates.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenMissing()
    {
        await using var db = CreateContext();
        var service = new ReminderTemplateService(db);

        Assert.False(await service.DeleteAsync(Guid.NewGuid()));
    }
}
