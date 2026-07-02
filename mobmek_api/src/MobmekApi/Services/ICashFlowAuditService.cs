using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface ICashFlowAuditService
{
    /// <summary>
    /// Queues an audit entry on the current unit of work — it commits in the caller's
    /// SaveChanges, so the trail can never record a change that didn't happen (or miss one
    /// that did). Callers pass field-level <paramref name="changes"/> for updates.
    /// </summary>
    void Record(string entityType, Guid entityId, string action, string summary, IReadOnlyList<AuditFieldChange>? changes = null);

    /// <summary>Paged trail, newest first.</summary>
    Task<CashFlowAuditPageDto> GetPagedAsync(CashFlowAuditFilter filter, CancellationToken cancellationToken = default);
}
