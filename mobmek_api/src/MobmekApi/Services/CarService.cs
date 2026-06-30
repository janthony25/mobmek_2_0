using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Services;

public class CarService(AppDbContext db) : ICarService
{
    public async Task<IReadOnlyList<CarDto>> GetAllAsync(Guid? customerId = null, CancellationToken cancellationToken = default)
    {
        var query = db.Cars.AsNoTracking();

        if (customerId is { } id)
        {
            query = query.Where(c => c.CustomerId == id);
        }

        return await query
            .OrderBy(c => c.Make)
            .ThenBy(c => c.Model)
            .Select(c => ToDto(c))
            .ToListAsync(cancellationToken);
    }

    public async Task<CarDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var car = await db.Cars
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return car is null ? null : ToDto(car);
    }

    public async Task<CarDto?> CreateAsync(CreateCarRequest request, CancellationToken cancellationToken = default)
    {
        var customerExists = await db.Customers.AnyAsync(c => c.Id == request.CustomerId, cancellationToken);
        if (!customerExists)
        {
            return null;
        }

        var car = new Car
        {
            CustomerId = request.CustomerId,
            Make = request.Make,
            Model = request.Model,
            Year = request.Year,
            Rego = request.Rego,
            Vin = request.Vin,
            Color = request.Color,
            EngineType = request.EngineType,
            Odometer = request.Odometer,
        };

        db.Cars.Add(car);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(car);
    }

    public async Task<CarDto?> UpdateAsync(Guid id, UpdateCarRequest request, CancellationToken cancellationToken = default)
    {
        var car = await db.Cars.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (car is null)
        {
            return null;
        }

        car.Make = request.Make;
        car.Model = request.Model;
        car.Year = request.Year;
        car.Rego = request.Rego;
        car.Vin = request.Vin;
        car.Color = request.Color;
        car.EngineType = request.EngineType;
        car.Odometer = request.Odometer;

        await db.SaveChangesAsync(cancellationToken);

        return ToDto(car);
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

    private static CarDto ToDto(Car c) =>
        new(c.Id, c.CustomerId, c.Make, c.Model, c.Year, c.Rego, c.Vin, c.Color, c.EngineType, c.Odometer, c.CreatedAtUtc, c.UpdatedAtUtc);
}
