using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one page of customers with the aggregates the list cards display.
    /// <paramref name="search"/> matches name, phone or email, case-insensitively.
    /// <paramref name="sortBy"/> is "newest" (default), "oldest", or "name". <paramref name="dateFrom"/>/
    /// <paramref name="dateTo"/> filter by the customer's created date (inclusive).
    /// </summary>
    Task<PagedResult<CustomerListItemDto>> GetPagedAsync(
        int page, int pageSize, string? search,
        string? sortBy = null, DateOnly? dateFrom = null, DateOnly? dateTo = null,
        CancellationToken cancellationToken = default);

    Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default);

    Task<CustomerDto?> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Soft duplicate check for the create/edit form — looks for an existing customer
    /// with the same phone and/or email (whitespace/case-insensitive), excluding
    /// <paramref name="excludeId"/> when editing. Returns an empty list when neither value is
    /// supplied. This is advisory only; callers are never blocked from saving on a match.</summary>
    Task<IReadOnlyList<CustomerDuplicateMatchDto>> CheckDuplicatesAsync(
        string? phone, string? email, Guid? excludeId, CancellationToken cancellationToken = default);
}
