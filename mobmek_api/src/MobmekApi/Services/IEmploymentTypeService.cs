using MobmekApi.DTOs;

namespace MobmekApi.Services;

public enum EmploymentTypeDeleteError
{
    None,
    NotFound,
    InUse,
}

public interface IEmploymentTypeService
{
    Task<IReadOnlyList<EmploymentTypeDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<EmploymentTypeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EmploymentTypeDto> CreateAsync(CreateEmploymentTypeRequest request, CancellationToken cancellationToken = default);

    Task<EmploymentTypeDto?> UpdateAsync(Guid id, UpdateEmploymentTypeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes an employment type. Returns <see cref="EmploymentTypeDeleteError.InUse"/>
    /// instead of deleting if any employee still references it — a DB-level <c>Restrict</c>
    /// foreign key, so this check surfaces a friendly error instead of a 500.</summary>
    Task<EmploymentTypeDeleteError> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
