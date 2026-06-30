using MobmekApi.DTOs;

namespace MobmekApi.Services;

public interface IEmploymentTypeService
{
    Task<IReadOnlyList<EmploymentTypeDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<EmploymentTypeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EmploymentTypeDto> CreateAsync(CreateEmploymentTypeRequest request, CancellationToken cancellationToken = default);

    Task<EmploymentTypeDto?> UpdateAsync(Guid id, UpdateEmploymentTypeRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
