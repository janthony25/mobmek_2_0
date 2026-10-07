using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class EmploymentTypeService(AppDbContext db) : IEmploymentTypeService
{
    public async Task<IReadOnlyList<EmploymentTypeDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.EmploymentTypes
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => ToDto(t))
            .ToListAsync(cancellationToken);
    }

    public async Task<EmploymentTypeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var type = await db.EmploymentTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        return type is null ? null : ToDto(type);
    }

    public async Task<EmploymentTypeDto> CreateAsync(CreateEmploymentTypeRequest request, CancellationToken cancellationToken = default)
    {
        var type = new EmploymentType { Name = request.Name };

        db.EmploymentTypes.Add(type);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(type);
    }

    public async Task<EmploymentTypeDto?> UpdateAsync(Guid id, UpdateEmploymentTypeRequest request, CancellationToken cancellationToken = default)
    {
        var type = await db.EmploymentTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (type is null)
        {
            return null;
        }

        type.Name = request.Name;
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(type);
    }

    public async Task<EmploymentTypeDeleteError> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var type = await db.EmploymentTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (type is null)
        {
            return EmploymentTypeDeleteError.NotFound;
        }

        if (await db.Employees.AnyAsync(e => e.EmploymentTypeId == id, cancellationToken))
        {
            return EmploymentTypeDeleteError.InUse;
        }

        db.EmploymentTypes.Remove(type);
        await db.SaveChangesAsync(cancellationToken);

        return EmploymentTypeDeleteError.None;
    }

    private static EmploymentTypeDto ToDto(EmploymentType t) =>
        new(t.Id, t.Name, t.CreatedAtUtc, t.UpdatedAtUtc);
}
