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
            c.Year, c.Rego, c.Vin, c.Color, c.EngineType, c.CreatedAtUtc, c.UpdatedAtUtc, c.UpdatedByName);

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
        var (car, error) = await BuildAsync(request, cancellationToken);
        if (error != CarWriteError.None)
        {
            return (null, error);
        }

        await db.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(car!.Id, cancellationToken), CarWriteError.None);
    }

    /// <summary>Validates and constructs a Car entity, adding it to the tracked context —
    /// deliberately does NOT call SaveChangesAsync. <see cref="CreateAsync"/> is just this plus
    /// a save; exposed separately so a caller needing car creation as part of a larger atomic
    /// operation (see <c>AppointmentService.ConvertToCarAsync</c>) can batch it into their own
    /// single SaveChangesAsync call instead of this method committing on its own.</summary>
    public async Task<(Car? Car, CarWriteError Error)> BuildAsync(CreateCarRequest request, CancellationToken cancellationToken = default)
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

        var uniqueError = await ValidateUniqueAsync(
            excludeCarId: null, request.Rego, currentRego: null, request.Vin, currentVin: null, cancellationToken);
        if (uniqueError != CarWriteError.None)
        {
            return (null, uniqueError);
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
        };

        db.Cars.Add(car);
        return (car, CarWriteError.None);
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

        var uniqueError = await ValidateUniqueAsync(
            excludeCarId: id, request.Rego, currentRego: car.Rego, request.Vin, currentVin: car.Vin, cancellationToken);
        if (uniqueError != CarWriteError.None)
        {
            return (null, uniqueError);
        }

        car.CarMakeId = request.CarMakeId;
        car.CarModelId = request.CarModelId;
        car.Year = request.Year;
        car.Rego = request.Rego;
        car.Vin = request.Vin;
        car.Color = request.Color;
        car.EngineType = request.EngineType;

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

    /// <summary>Rego is always checked (required, case/whitespace-insensitive) when it's actually
    /// changing; VIN the same, only when provided (most cars don't have one on file, and two
    /// blanks shouldn't collide). Hard block, not a warning — unlike customer phone/email, a
    /// duplicate rego/VIN is unambiguously a data-entry mistake (one physical car, one record).
    /// <paramref name="currentRego"/>/<paramref name="currentVin"/> are null on create (always
    /// check) or the car's existing values on update — if the incoming value matches what's
    /// already on this car, skip the check entirely. Without this, editing an unrelated field
    /// (color, year) on either of a pre-existing duplicate-rego pair — confirmed to exist in
    /// production from the legacy import, 7 pairs as of 2026-10-07 — would wrongly be blocked
    /// as "colliding with itself's twin" even though the rego value itself isn't changing.</summary>
    private async Task<CarWriteError> ValidateUniqueAsync(
        Guid? excludeCarId, string rego, string? currentRego, string? vin, string? currentVin, CancellationToken cancellationToken)
    {
        var query = db.Cars.AsNoTracking().AsQueryable();
        if (excludeCarId is { } id)
        {
            query = query.Where(c => c.Id != id);
        }

        var normalizedRego = rego.Trim().ToUpper();
        var regoChanging = currentRego is null || !string.Equals(currentRego.Trim(), normalizedRego, StringComparison.OrdinalIgnoreCase);
        if (regoChanging && await query.AnyAsync(c => c.Rego.ToUpper() == normalizedRego, cancellationToken))
        {
            return CarWriteError.DuplicateRego;
        }

        if (!string.IsNullOrWhiteSpace(vin))
        {
            var normalizedVin = vin.Trim().ToUpper();
            var vinChanging = currentVin is null || !string.Equals(currentVin.Trim(), normalizedVin, StringComparison.OrdinalIgnoreCase);
            if (vinChanging && await query.AnyAsync(c => c.Vin != null && c.Vin.ToUpper() == normalizedVin, cancellationToken))
            {
                return CarWriteError.DuplicateVin;
            }
        }

        return CarWriteError.None;
    }
}
