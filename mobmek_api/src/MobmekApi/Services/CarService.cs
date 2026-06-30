using System.Linq.Expressions;
using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class CarService(AppDbContext db) : ICarService
{
    // Inline projection so EF resolves the make/model names via joins.
    private static readonly Expression<Func<Car, CarDto>> ToDto =
        c => new CarDto(
            c.Id, c.CustomerId, c.CarMakeId, c.CarMake!.Name, c.CarModelId, c.CarModel!.Name,
            c.Year, c.Rego, c.Vin, c.Color, c.EngineType, c.Odometer, c.CreatedAtUtc, c.UpdatedAtUtc);

    public async Task<IReadOnlyList<CarDto>> GetAllAsync(Guid? customerId = null, CancellationToken cancellationToken = default)
    {
        var query = db.Cars.AsNoTracking();

        if (customerId is { } id)
        {
            query = query.Where(c => c.CustomerId == id);
        }

        return await query
            .OrderBy(c => c.CarMake!.Name)
            .ThenBy(c => c.CarModel!.Name)
            .Select(ToDto)
            .ToListAsync(cancellationToken);
    }

    public async Task<CarDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await db.Cars
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(CarDto? Car, CarWriteError Error)> CreateAsync(CreateCarRequest request, CancellationToken cancellationToken = default)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == request.CustomerId, cancellationToken))
        {
            return (null, CarWriteError.CustomerNotFound);
        }

        var makeModelError = await ValidateMakeModelAsync(request.CarMakeId, request.CarModelId, cancellationToken);
        if (makeModelError != CarWriteError.None)
        {
            return (null, makeModelError);
        }

        var car = new Car
        {
            CustomerId = request.CustomerId,
            CarMakeId = request.CarMakeId,
            CarModelId = request.CarModelId,
            Year = request.Year,
            Rego = request.Rego,
            Vin = request.Vin,
            Color = request.Color,
            EngineType = request.EngineType,
            Odometer = request.Odometer,
        };

        db.Cars.Add(car);
        await db.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(car.Id, cancellationToken), CarWriteError.None);
    }

    public async Task<(CarDto? Car, CarWriteError Error)> UpdateAsync(Guid id, UpdateCarRequest request, CancellationToken cancellationToken = default)
    {
        var car = await db.Cars.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (car is null)
        {
            return (null, CarWriteError.NotFound);
        }

        var makeModelError = await ValidateMakeModelAsync(request.CarMakeId, request.CarModelId, cancellationToken);
        if (makeModelError != CarWriteError.None)
        {
            return (null, makeModelError);
        }

        car.CarMakeId = request.CarMakeId;
        car.CarModelId = request.CarModelId;
        car.Year = request.Year;
        car.Rego = request.Rego;
        car.Vin = request.Vin;
        car.Color = request.Color;
        car.EngineType = request.EngineType;
        car.Odometer = request.Odometer;

        await db.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(car.Id, cancellationToken), CarWriteError.None);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var car = await db.Cars.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (car is null)
        {
            return false;
        }

        db.Cars.Remove(car);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<CarWriteError> ValidateMakeModelAsync(Guid makeId, Guid modelId, CancellationToken cancellationToken)
    {
        if (!await db.CarMakes.AnyAsync(m => m.Id == makeId, cancellationToken))
        {
            return CarWriteError.MakeNotFound;
        }

        var model = await db.CarModels.AsNoTracking().FirstOrDefaultAsync(m => m.Id == modelId, cancellationToken);
        if (model is null)
        {
            return CarWriteError.ModelNotFound;
        }

        if (model.CarMakeId != makeId)
        {
            return CarWriteError.ModelNotInMake;
        }

        return CarWriteError.None;
    }
}
