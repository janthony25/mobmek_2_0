using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface IEmployeeTitleService
{
    Task<IReadOnlyList<EmployeeTitleDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<EmployeeTitleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EmployeeTitleDto> CreateAsync(CreateEmployeeTitleRequest request, CancellationToken cancellationToken = default);

    Task<EmployeeTitleDto?> UpdateAsync(Guid id, UpdateEmployeeTitleRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
