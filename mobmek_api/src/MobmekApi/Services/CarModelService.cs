using System.Linq.Expressions;
using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class CarModelService(AppDbContext db) : ICarModelService
{
    // Inline projection so EF resolves the make name via a join.
    private static readonly Expression<Func<CarModel, CarModelDto>> ToDto =
        m => new CarModelDto(m.Id, m.CarMakeId, m.CarMake!.Name, m.Name, m.CreatedAtUtc, m.UpdatedAtUtc);

    public async Task<IReadOnlyList<CarModelDto>> GetAllAsync(Guid? makeId = null, CancellationToken cancellationToken = default)
    {
        var query = db.CarModels.AsNoTracking();

        if (makeId is { } id)
        {
            query = query.Where(m => m.CarMakeId == id);
        }

        return await query
            .OrderBy(m => m.Name)
            .Select(ToDto)
            .ToListAsync(cancellationToken);
    }

    public async Task<CarModelDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await db.CarModels
            .AsNoTracking()
            .Where(m => m.Id == id)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CarModelDto?> CreateAsync(CreateCarModelRequest request, CancellationToken cancellationToken = default)
    {
        if (!await db.CarMakes.AnyAsync(m => m.Id == request.CarMakeId, cancellationToken))
        {
            return null;
        }

        var model = new CarModel { CarMakeId = request.CarMakeId, Name = request.Name };

        db.CarModels.Add(model);
        await db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(model.Id, cancellationToken);
    }

    public async Task<(CarModelDto? Model, bool MakeMissing)> UpdateAsync(Guid id, UpdateCarModelRequest request, CancellationToken cancellationToken = default)
    {
        var model = await db.CarModels.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (model is null)
        {
            return (null, false);
        }

        if (!await db.CarMakes.AnyAsync(m => m.Id == request.CarMakeId, cancellationToken))
        {
            return (null, true);
        }

        model.CarMakeId = request.CarMakeId;
        model.Name = request.Name;
        await db.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(model.Id, cancellationToken), false);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var model = await db.CarModels.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (model is null)
        {
            return false;
        }

        db.CarModels.Remove(model);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }
}
