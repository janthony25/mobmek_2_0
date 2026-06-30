using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class GstSettingService(AppDbContext db) : IGstSettingService
{
    public async Task<GstSettingDto> GetCurrentAsync(CancellationToken cancellationToken = default) =>
        ToDto(await GetOrCreateAsync(cancellationToken));

    public async Task<GstSettingDto> UpdateAsync(decimal rate, CancellationToken cancellationToken = default)
    {
        var setting = await GetOrCreateAsync(cancellationToken);
        setting.Rate = rate;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(setting);
    }

    // The GST setting is a singleton: return the existing row, or create the default one on first use.
    private async Task<GstSetting> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        var setting = await db.GstSettings.OrderBy(g => g.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (setting is null)
        {
            setting = new GstSetting { Rate = 0.15m };
            db.GstSettings.Add(setting);
            await db.SaveChangesAsync(cancellationToken);
        }

        return setting;
    }

    private static GstSettingDto ToDto(GstSetting g) => new(g.Id, g.Rate, g.CreatedAtUtc, g.UpdatedAtUtc);
}
