namespace MobmekApi.DTOs;

/// <summary>One before/after field change inside an audit entry.</summary>
public record AuditFieldChange(string Field, string? Old, string? New);

/// <summary>One audit-trail entry; <see cref="Changes"/> is the parsed field-level diff for updates.</summary>
public record CashFlowAuditLogDto(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string Action,
    string Summary,
    IReadOnlyList<AuditFieldChange>? Changes,
    DateTime TimestampUtc);

public record CashFlowAuditPageDto(
    IReadOnlyList<CashFlowAuditLogDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

/// <summary>Filter for the audit trail; all criteria optional, newest first.</summary>
public record CashFlowAuditFilter(
    string? EntityType,
    Guid? EntityId,
    DateTime? From,
    DateTime? To,
    int Page = 1,
    int PageSize = 50);
