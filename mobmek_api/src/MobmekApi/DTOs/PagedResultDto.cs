namespace MobmekApi.DTOs;

/// <summary>One page of a list plus the total row count, so clients can build pagers.</summary>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
