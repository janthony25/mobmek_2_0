using MobmekApi.DTOs;

namespace MobmekApi.Services;

/// <summary>
/// Builds the Included/Excluded GST review report (design note: this is a reconciliation
/// view only — both totals are informational and neither changes what's actually filed).
/// </summary>
public interface IGstReportService
{
    Task<GstReportDto> GetReportAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken = default);
}
