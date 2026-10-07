using MobmekApi.DTOs;

namespace MobmekApi.Services;

/// <summary>Outcome of a create/update that depends on referenced lookup records existing.</summary>
public enum EmployeeWriteError
{
    None,
    NotFound,
    TitleNotFound,
    EmploymentTypeNotFound,
    InUse,
}

public interface IEmployeeService
{
    Task<IReadOnlyList<EmployeeDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Name-only list for pickers — open to any signed-in staff member, not just
    /// <see cref="Permissions.ManageEmployees"/>. See <see cref="DTOs.EmployeeSummaryDto"/>.</summary>
    Task<IReadOnlyList<EmployeeSummaryDto>> GetSummariesAsync(CancellationToken cancellationToken = default);

    Task<EmployeeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(EmployeeDto? Employee, EmployeeWriteError Error)> CreateAsync(CreateEmployeeRequest request, CancellationToken cancellationToken = default);

    Task<(EmployeeDto? Employee, EmployeeWriteError Error)> UpdateAsync(Guid id, UpdateEmployeeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes an employee. Returns <see cref="EmployeeWriteError.InUse"/> instead of
    /// deleting if the employee still has a login account or is assigned to a job as a mechanic —
    /// both are DB-level <c>Restrict</c> foreign keys, so this check exists to surface a friendly
    /// error instead of letting a <c>DbUpdateException</c> turn into a 500.</summary>
    Task<EmployeeWriteError> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
