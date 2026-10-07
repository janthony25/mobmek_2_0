using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class EmailTemplateServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task GetAllAsync_SeedsAllThreeKeys_OnFirstRead()
    {
        await using var db = CreateContext();
        var service = new EmailTemplateService(db);

        var templates = await service.GetAllAsync();

        Assert.Equal(3, templates.Count);
        Assert.Equal(EmailTemplateKeys.All, templates.Select(t => t.Key).ToArray());
        Assert.All(templates, t => Assert.True(t.IsSystem));
        Assert.Equal(3, await db.EmailTemplates.CountAsync());
    }

    [Fact]
    public async Task GetAllAsync_IsIdempotent_DoesNotDuplicateOnSecondCall()
    {
        await using var db = CreateContext();
        var service = new EmailTemplateService(db);

        await service.GetAllAsync();
        await service.GetAllAsync();

        Assert.Equal(3, await db.EmailTemplates.CountAsync());
    }

    [Fact]
    public async Task GetByKeyAsync_UnknownKey_ReturnsNull()
    {
        await using var db = CreateContext();
        var service = new EmailTemplateService(db);

        var template = await service.GetByKeyAsync("NotARealKey");

        Assert.Null(template);
    }

    [Fact]
    public async Task UpdateAsync_ChangesWording_AndPersists()
    {
        await using var db = CreateContext();
        var service = new EmailTemplateService(db);

        var updated = await service.UpdateAsync(
            EmailTemplateKeys.ReminderDue,
            new UpdateEmailTemplateRequest("Custom subject {{BusinessName}}", "Custom intro {{CustomerName}}"));

        Assert.NotNull(updated);
        Assert.Equal("Custom subject {{BusinessName}}", updated!.SubjectTemplate);
        Assert.Equal("Custom intro {{CustomerName}}", updated.BodyIntroTemplate);

        var reloaded = await service.GetByKeyAsync(EmailTemplateKeys.ReminderDue);
        Assert.Equal("Custom subject {{BusinessName}}", reloaded!.SubjectTemplate);
    }

    [Fact]
    public async Task UpdateAsync_UnknownKey_ReturnsNull()
    {
        await using var db = CreateContext();
        var service = new EmailTemplateService(db);

        var updated = await service.UpdateAsync("NotARealKey", new UpdateEmailTemplateRequest("x", "y"));

        Assert.Null(updated);
    }

    [Fact]
    public async Task PreviewAsync_RendersStoredWording_WithDummyTokens()
    {
        await using var db = CreateContext();
        var service = new EmailTemplateService(db);
        await service.UpdateAsync(
            EmailTemplateKeys.InvoiceSend,
            new UpdateEmailTemplateRequest("{{DocumentLabel}} {{DocumentNumber}}", "Hi {{CustomerName}}"));

        var preview = await service.PreviewAsync(EmailTemplateKeys.InvoiceSend);

        Assert.NotNull(preview);
        Assert.Equal("Invoice INV-0001", preview!.Subject);
        Assert.Equal("Hi Jane Doe", preview.BodyIntro);
    }

    [Fact]
    public async Task PreviewAsync_UnknownKey_ReturnsNull()
    {
        await using var db = CreateContext();
        var service = new EmailTemplateService(db);

        var preview = await service.PreviewAsync("NotARealKey");

        Assert.Null(preview);
    }
}
