using MobmekApi.DTOs;

namespace MobmekApi.Services;

/// <summary>Outcome of a create/update that depends on referenced lookup records existing.</summary>
public enum EmployeeWriteError
{
    None,
    NotFound,
    TitleNotFound,
    EmploymentTypeNotFound,
}

public interface IEmployeeService
{
    Task<IReadOnlyList<EmployeeDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<EmployeeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(EmployeeDto? Employee, EmployeeWriteError Error)> CreateAsync(CreateEmployeeRequest request, CancellationToken cancellationToken = default);

    Task<(EmployeeDto? Employee, EmployeeWriteError Error)> UpdateAsync(Guid id, UpdateEmployeeRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
