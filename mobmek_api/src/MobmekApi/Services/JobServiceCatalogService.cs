using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class JobServiceCatalogService(AppDbContext db) : IJobServiceCatalogService
{
    public async Task<IReadOnlyList<JobServiceDto>> GetAllAsync(bool? activeOnly = null, CancellationToken cancellationToken = default)
    {
        var query = db.JobServices.AsNoTracking();

        if (activeOnly == true)
        {
            query = query.Where(s => s.IsActive);
        }

        return await query
            .OrderBy(s => s.Name)
            .Select(s => ToDto(s))
            .ToListAsync(cancellationToken);
    }

    public async Task<JobServiceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var service = await db.JobServices
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        return service is null ? null : ToDto(service);
    }

    public async Task<JobServiceDto> CreateAsync(CreateJobServiceRequest request, CancellationToken cancellationToken = default)
    {
        var service = new Entities.JobService
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            IsActive = request.IsActive,
        };

        db.JobServices.Add(service);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(service);
    }

    public async Task<JobServiceDto?> UpdateAsync(Guid id, UpdateJobServiceRequest request, CancellationToken cancellationToken = default)
    {
        var service = await db.JobServices.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (service is null)
        {
            return null;
        }

        service.Name = request.Name;
        service.Description = request.Description;
        service.Price = request.Price;
        service.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);

        return ToDto(service);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var service = await db.JobServices.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (service is null)
        {
            return false;
        }

        db.JobServices.Remove(service);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static JobServiceDto ToDto(Entities.JobService s) =>
        new(s.Id, s.Name, s.Description, s.Price, s.IsActive, s.CreatedAtUtc, s.UpdatedAtUtc);
}
