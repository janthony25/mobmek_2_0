using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class CarMakeService(AppDbContext db) : ICarMakeService
{
    public async Task<IReadOnlyList<CarMakeDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.CarMakes
            .AsNoTracking()
            .OrderBy(m => m.Name)
            .Select(m => ToDto(m))
            .ToListAsync(cancellationToken);
    }

    public async Task<CarMakeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var make = await db.CarMakes.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        return make is null ? null : ToDto(make);
    }

    public async Task<CarMakeDto> CreateAsync(CreateCarMakeRequest request, CancellationToken cancellationToken = default)
    {
        var make = new CarMake { Name = request.Name };

        db.CarMakes.Add(make);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(make);
    }

    public async Task<CarMakeDto?> UpdateAsync(Guid id, UpdateCarMakeRequest request, CancellationToken cancellationToken = default)
    {
        var make = await db.CarMakes.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (make is null)
        {
            return null;
        }

        make.Name = request.Name;
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(make);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var make = await db.CarMakes.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (make is null)
        {
            return false;
        }

        db.CarMakes.Remove(make);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static CarMakeDto ToDto(CarMake m) =>
        new(m.Id, m.Name, m.CreatedAtUtc, m.UpdatedAtUtc);
}
