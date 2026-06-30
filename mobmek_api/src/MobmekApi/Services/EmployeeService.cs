using System.Linq.Expressions;
using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class EmployeeService(AppDbContext db) : IEmployeeService
{
    // Inline projection (expression tree) so EF can resolve the Title/EmploymentType
    // navigation names via a join — a ToDto() method call would not translate.
    private static readonly Expression<Func<Employee, EmployeeDto>> ToDto =
        e => new EmployeeDto(
            e.Id, e.FirstName, e.LastName,
            e.TitleId, e.Title!.Name,
            e.EmploymentTypeId, e.EmploymentType!.Name,
            e.ContactNumber, e.EmailAddress, e.PhysicalAddress,
            e.CreatedAtUtc, e.UpdatedAtUtc);

    public async Task<IReadOnlyList<EmployeeDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.Employees
            .AsNoTracking()
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .Select(ToDto)
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployeeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await db.Employees
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(EmployeeDto? Employee, EmployeeWriteError Error)> CreateAsync(CreateEmployeeRequest request, CancellationToken cancellationToken = default)
    {
        var referenceError = await ValidateReferencesAsync(request.TitleId, request.EmploymentTypeId, cancellationToken);
        if (referenceError != EmployeeWriteError.None)
        {
            return (null, referenceError);
        }

        var employee = new Employee
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            TitleId = request.TitleId,
            EmploymentTypeId = request.EmploymentTypeId,
            ContactNumber = request.ContactNumber,
            EmailAddress = request.EmailAddress,
            PhysicalAddress = request.PhysicalAddress,
        };

        db.Employees.Add(employee);
        await db.SaveChangesAsync(cancellationToken);

        return ((await GetByIdAsync(employee.Id, cancellationToken))!, EmployeeWriteError.None);
    }

    public async Task<(EmployeeDto? Employee, EmployeeWriteError Error)> UpdateAsync(Guid id, UpdateEmployeeRequest request, CancellationToken cancellationToken = default)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (employee is null)
        {
            return (null, EmployeeWriteError.NotFound);
        }

        var referenceError = await ValidateReferencesAsync(request.TitleId, request.EmploymentTypeId, cancellationToken);
        if (referenceError != EmployeeWriteError.None)
        {
            return (null, referenceError);
        }

        employee.FirstName = request.FirstName;
        employee.LastName = request.LastName;
        employee.TitleId = request.TitleId;
        employee.EmploymentTypeId = request.EmploymentTypeId;
        employee.ContactNumber = request.ContactNumber;
        employee.EmailAddress = request.EmailAddress;
        employee.PhysicalAddress = request.PhysicalAddress;

        await db.SaveChangesAsync(cancellationToken);

        return ((await GetByIdAsync(employee.Id, cancellationToken))!, EmployeeWriteError.None);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (employee is null)
        {
            return false;
        }

        db.Employees.Remove(employee);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<EmployeeWriteError> ValidateReferencesAsync(Guid titleId, Guid employmentTypeId, CancellationToken cancellationToken)
    {
        if (!await db.EmployeeTitles.AnyAsync(t => t.Id == titleId, cancellationToken))
        {
            return EmployeeWriteError.TitleNotFound;
        }

        if (!await db.EmploymentTypes.AnyAsync(t => t.Id == employmentTypeId, cancellationToken))
        {
            return EmployeeWriteError.EmploymentTypeNotFound;
        }

        return EmployeeWriteError.None;
    }
}
