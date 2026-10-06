using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Controllers;

/// <summary>Diagnostics and on-demand reconcile for the Google Calendar sync (§6 of the design doc).</summary>
[ApiController]
[Route("api/calendarsync")]
[Produces("application/json")]
[Authorize(Policy = Permissions.ManageCalendarSync)]
public class CalendarSyncController(AppDbContext db, IGoogleCalendarClient calendarClient, CalendarSyncJob syncJob, CalendarSyncStatus status)
    : ControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType(typeof(CalendarSyncStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CalendarSyncStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        return Ok(await BuildStatusAsync(cancellationToken));
    }

    /// <summary>
    /// Runs the reconcile pass now instead of waiting for the hourly tick. With
    /// <paramref name="backfill"/> = true, also runs the one-time catch-up first — the whole
    /// go-live flow is configure (§7) then a single call here.
    /// </summary>
    [HttpPost("reconcile")]
    [ProducesResponseType(typeof(CalendarSyncStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CalendarSyncStatusDto>> Reconcile([FromQuery] bool backfill, CancellationToken cancellationToken)
    {
        await syncJob.ReconcileAsync(backfill, cancellationToken);
        return Ok(await BuildStatusAsync(cancellationToken));
    }

    private async Task<CalendarSyncStatusDto> BuildStatusAsync(CancellationToken cancellationToken)
    {
        var pending = await db.CalendarSyncItems.AsNoTracking().ToListAsync(cancellationToken);
        var (lastReconcileUtc, lastReconcileError) = status.Snapshot();

        return new CalendarSyncStatusDto(
            calendarClient.IsConfigured,
            pending.Count,
            pending.Count > 0 ? pending.Min(i => i.CreatedAtUtc) : null,
            lastReconcileUtc,
            lastReconcileError);
    }
}
