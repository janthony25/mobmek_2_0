using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class EmployeeTitleService(AppDbContext db) : IEmployeeTitleService
{
    public async Task<IReadOnlyList<EmployeeTitleDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.EmployeeTitles
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => ToDto(t))
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployeeTitleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var title = await db.EmployeeTitles
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        return title is null ? null : ToDto(title);
    }

    public async Task<EmployeeTitleDto> CreateAsync(CreateEmployeeTitleRequest request, CancellationToken cancellationToken = default)
    {
        var title = new EmployeeTitle { Name = request.Name };

        db.EmployeeTitles.Add(title);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(title);
    }

    public async Task<EmployeeTitleDto?> UpdateAsync(Guid id, UpdateEmployeeTitleRequest request, CancellationToken cancellationToken = default)
    {
        var title = await db.EmployeeTitles.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (title is null)
        {
            return null;
        }

        title.Name = request.Name;
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(title);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var title = await db.EmployeeTitles.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (title is null)
        {
            return false;
        }

        db.EmployeeTitles.Remove(title);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static EmployeeTitleDto ToDto(EmployeeTitle t) =>
        new(t.Id, t.Name, t.CreatedAtUtc, t.UpdatedAtUtc);
}
