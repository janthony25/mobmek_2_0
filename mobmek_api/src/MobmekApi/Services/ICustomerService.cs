using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default);

    Task<CustomerDto?> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
