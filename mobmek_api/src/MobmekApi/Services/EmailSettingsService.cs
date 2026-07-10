using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class EmailSettingsService(AppDbContext db, IConfiguration configuration) : IEmailSettingsService
{
    public async Task<EmailSettingsDto> GetCurrentAsync(CancellationToken cancellationToken = default) =>
        ToDto(await GetOrCreateAsync(cancellationToken));

    public async Task<EmailSettingsDto> UpdateAsync(UpdateEmailSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateAsync(cancellationToken);
        settings.FromName = request.FromName;
        settings.FromAddress = request.FromAddress;
        settings.ReplyToAddress = request.ReplyToAddress;
        settings.BccSelf = request.BccSelf;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(settings);
    }

    private async Task<EmailSettings> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        var settings = await db.EmailSettings.OrderBy(s => s.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = new EmailSettings();
            db.EmailSettings.Add(settings);
            await db.SaveChangesAsync(cancellationToken);
        }

        return settings;
    }

    private EmailSettingsDto ToDto(EmailSettings s) => new(
        s.Id, s.FromName, s.FromAddress, s.ReplyToAddress, s.BccSelf,
        ResendConfigured: !string.IsNullOrWhiteSpace(configuration["Email:Resend:ApiKey"]),
        s.CreatedAtUtc, s.UpdatedAtUtc, s.UpdatedByName);
}
