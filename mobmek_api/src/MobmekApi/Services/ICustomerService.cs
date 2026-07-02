using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one page of customers (ordered by last then first name) with the aggregates the
    /// list cards display. <paramref name="search"/> matches name, phone or email, case-insensitively.
    /// </summary>
    Task<PagedResult<CustomerListItemDto>> GetPagedAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default);

    Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default);

    Task<CustomerDto?> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
