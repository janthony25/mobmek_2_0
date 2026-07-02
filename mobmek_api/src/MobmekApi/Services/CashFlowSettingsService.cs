using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class CashFlowSettingsService(AppDbContext db, ICashFlowAuditService audit) : ICashFlowSettingsService
{
    public async Task<CashFlowSettingsDto> GetCurrentAsync(CancellationToken cancellationToken = default) =>
        ToDto(await GetOrCreateAsync(cancellationToken));

    public async Task<CashFlowSettingsDto?> UpdateAsync(UpdateCashFlowSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var referenced = new[] { request.DefaultAccountId, request.CashAccountId, request.CardAccountId, request.BankTransferAccountId }
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (referenced.Count > 0)
        {
            var known = await db.CashAccounts.CountAsync(a => referenced.Contains(a.Id), cancellationToken);
            if (known != referenced.Count)
            {
                return null;
            }
        }

        var settings = await GetOrCreateAsync(cancellationToken);

        var changes = new List<AuditFieldChange>();
        AuditDiff.Add(changes, "Default account", settings.DefaultAccountId, request.DefaultAccountId);
        AuditDiff.Add(changes, "Cash account", settings.CashAccountId, request.CashAccountId);
        AuditDiff.Add(changes, "Card account", settings.CardAccountId, request.CardAccountId);
        AuditDiff.Add(changes, "Bank transfer account", settings.BankTransferAccountId, request.BankTransferAccountId);
        AuditDiff.Add(changes, "Safety buffer", settings.SafetyBufferAmount, request.SafetyBufferAmount);
        AuditDiff.Add(changes, "Lock date", settings.LockDate, request.LockDate);

        settings.DefaultAccountId = request.DefaultAccountId;
        settings.CashAccountId = request.CashAccountId;
        settings.CardAccountId = request.CardAccountId;
        settings.BankTransferAccountId = request.BankTransferAccountId;
        settings.SafetyBufferAmount = request.SafetyBufferAmount;
        settings.LockDate = request.LockDate;

        if (changes.Count > 0)
        {
            audit.Record("CashFlowSettings", settings.Id, "Updated", AuditDiff.Summarize(changes), changes);
        }

        await db.SaveChangesAsync(cancellationToken);

        return ToDto(settings);
    }

    // Settings is a singleton: return the existing row, or create the (all-unset) default on first use.
    private async Task<CashFlowSettings> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        var settings = await db.CashFlowSettings.OrderBy(s => s.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = new CashFlowSettings();
            db.CashFlowSettings.Add(settings);
            await db.SaveChangesAsync(cancellationToken);
        }

        return settings;
    }

    private static CashFlowSettingsDto ToDto(CashFlowSettings s) =>
        new(s.Id, s.DefaultAccountId, s.CashAccountId, s.CardAccountId, s.BankTransferAccountId,
            s.SafetyBufferAmount, s.LockDate, s.CreatedAtUtc, s.UpdatedAtUtc);
}
