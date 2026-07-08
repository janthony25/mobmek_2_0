using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MobmekApi.Controllers;

[ApiController]
[Route("api/cash-flow-audit")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
public class CashFlowAuditController(ICashFlowAuditService auditService) : ControllerBase
{
    /// <summary>The audit trail, newest first; filter by entity type/id and time window.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(CashFlowAuditPageDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CashFlowAuditPageDto>> GetPaged(
        [FromQuery] string? entityType, [FromQuery] Guid? entityId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await auditService.GetPagedAsync(
            new CashFlowAuditFilter(entityType, entityId, from, to, page, pageSize), cancellationToken);
        return Ok(result);
    }
}
