using MobmekApi.DTOs;

namespace MobmekApi.Services;

public enum EmployeeTitleDeleteError
{
    None,
    NotFound,
    InUse,
}

public interface IEmployeeTitleService
{
    Task<IReadOnlyList<EmployeeTitleDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<EmployeeTitleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EmployeeTitleDto> CreateAsync(CreateEmployeeTitleRequest request, CancellationToken cancellationToken = default);

    Task<EmployeeTitleDto?> UpdateAsync(Guid id, UpdateEmployeeTitleRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes a title. Returns <see cref="EmployeeTitleDeleteError.InUse"/> instead of
    /// deleting if any employee still references it — a DB-level <c>Restrict</c> foreign key, so
    /// this check surfaces a friendly error instead of a <c>DbUpdateException</c>-turned-500.</summary>
    Task<EmployeeTitleDeleteError> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
