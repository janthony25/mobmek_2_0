using System.Text.Json;
using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class CashFlowAuditService(AppDbContext db) : ICashFlowAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Record(string entityType, Guid entityId, string action, string summary, IReadOnlyList<AuditFieldChange>? changes = null)
    {
        db.CashFlowAuditLogs.Add(new CashFlowAuditLog
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Summary = summary.Length <= 1000 ? summary : summary[..997] + "...",
            Changes = changes is { Count: > 0 } ? JsonSerializer.Serialize(changes, JsonOptions) : null,
        });
    }

    public async Task<CashFlowAuditPageDto> GetPagedAsync(CashFlowAuditFilter filter, CancellationToken cancellationToken = default)
    {
        var query = db.CashFlowAuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            query = query.Where(a => a.EntityType == filter.EntityType);
        }

        if (filter.EntityId is not null)
        {
            query = query.Where(a => a.EntityId == filter.EntityId);
        }

        if (filter.From is not null)
        {
            query = query.Where(a => a.CreatedAtUtc >= filter.From);
        }

        if (filter.To is not null)
        {
            query = query.Where(a => a.CreatedAtUtc <= filter.To);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);

        var items = await query
            .OrderByDescending(a => a.CreatedAtUtc).ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new CashFlowAuditPageDto(items.Select(ToDto).ToList(), page, pageSize, totalCount);
    }

    private static CashFlowAuditLogDto ToDto(CashFlowAuditLog a) =>
        new(a.Id, a.EntityType, a.EntityId, a.Action, a.Summary,
            a.Changes is null ? null : JsonSerializer.Deserialize<List<AuditFieldChange>>(a.Changes, JsonOptions),
            a.CreatedAtUtc);
}
