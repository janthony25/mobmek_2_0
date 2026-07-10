using MobmekApi.Entities;
using MobmekApi.LegacyImport.Legacy;
using MobmekApi.LegacyImport.Mappers;
using MobmekApi.LegacyImport.Pipeline;

namespace MobmekApi.LegacyImport.Phases;

/// <summary>
/// Imports legacy Cars (design §3.2). Make/model resolve through the lookup tables via
/// <see cref="MakeModelResolver"/> (first make by lowest MakeId; "Unknown" fallbacks).
/// Runs after <see cref="CustomerImportPhase"/> — owners must already be mapped.
/// </summary>
public sealed class CarImportPhase : ImportPhase
{
    public const string EntityType = "Car";

    public override string Name => "cars";

    public override async Task RunAsync(ImportContext context, LegacyDbReader source, CancellationToken ct)
    {
        var stats = context.StatsFor(Name);
        var makesById = (await source.MakesAsync(ct)).ToDictionary(m => m.MakeId);
        var makeIdsByCar = (await source.CarMakesAsync(ct))
            .GroupBy(j => j.CarId)
            .ToDictionary(g => g.Key, g => g.Select(j => j.MakeId).OrderBy(id => id).ToList());
        var resolver = await MakeModelResolver.LoadAsync(context.Db, ct);
        var legacyCars = await source.CarsAsync(ct);

        FlagDuplicateRegos(context, legacyCars);

        foreach (var legacy in legacyCars)
        {
            if (context.Map.Contains(EntityType, legacy.CarId))
            {
                stats.Skipped++;
                continue;
            }

            var carRef = $"Car #{legacy.CarId} ({legacy.CarRego.Trim()})";

            CarMake make;
            if (makeIdsByCar.TryGetValue(legacy.CarId, out var makeIds) && makeIds.Count > 0)
            {
                make = resolver.GetOrCreateMake(makesById[makeIds[0]].MakeName);
                if (makeIds.Count > 1)
                {
                    var names = string.Join(", ", makeIds.Select(id => makesById[id].MakeName));
                    context.Flag("car-multiple-makes", carRef, $"Had makes [{names}] — kept '{make.Name}'");
                }
            }
            else
            {
                make = resolver.GetOrCreateMake(null);
                context.Flag("car-no-make", carRef, $"No make in legacy data — set to '{MakeModelResolver.UnknownName}'");
            }

            if (string.IsNullOrWhiteSpace(legacy.CarModel))
            {
                context.Flag("car-no-model", carRef, $"No model in legacy data — set to '{MakeModelResolver.UnknownName}'");
            }

            var model = resolver.GetOrCreateModel(make, legacy.CarModel);

            if (legacy.CarYear is null)
            {
                context.Flag("car-no-year", carRef, "No year in legacy data — set to 0, fix manually");
            }

            var car = new Car
            {
                Rego = legacy.CarRego.Trim(),
                CarMakeId = make.Id,
                CarModelId = model.Id,
                Year = legacy.CarYear ?? 0,
                CustomerId = context.Map.Get(CustomerImportPhase.EntityType, legacy.CustomerId),
                CreatedAtUtc = NzTime.ToUtc(legacy.DateAdded),
                UpdatedAtUtc = NzTime.ToUtc(legacy.DateEdited),
            };

            context.Db.Cars.Add(car);
            await context.Map.AddAsync(EntityType, legacy.CarId, car.Id, ct);
            stats.Imported++;
        }
    }

    private static void FlagDuplicateRegos(ImportContext context, List<LegacyCar> cars)
    {
        var groups = cars
            .GroupBy(c => c.CarRego.Trim().ToUpperInvariant())
            .Where(g => g.Count() > 1);
        foreach (var group in groups)
        {
            context.Flag(
                "duplicate-rego",
                string.Join(", ", group.Select(c => $"#{c.CarId}")),
                $"Rego '{group.First().CarRego.Trim()}' appears {group.Count()} times — review whether they are the same vehicle");
        }
    }
}
