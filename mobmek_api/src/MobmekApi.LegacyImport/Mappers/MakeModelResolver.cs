using Microsoft.EntityFrameworkCore;
using MobmekApi.Data;
using MobmekApi.Entities;

namespace MobmekApi.LegacyImport.Mappers;

/// <summary>
/// Find-or-create for the CarMake/CarModel lookup tables (design §3.2). Matching is
/// case-insensitive so legacy uppercase values ("HILUX") reuse seeded rows ("Hilux")
/// instead of violating the unique name indexes. Created entities are tracked on the
/// context and cached here, so repeated requests within a run return the same instance.
/// </summary>
public sealed class MakeModelResolver
{
    public const string UnknownName = "Unknown";

    private readonly AppDbContext _db;
    private readonly Dictionary<string, CarMake> _makesByName;
    private readonly Dictionary<(Guid MakeId, string Model), CarModel> _modelsByMakeAndName;

    private MakeModelResolver(AppDbContext db, List<CarMake> makes, List<CarModel> models)
    {
        _db = db;
        _makesByName = makes.ToDictionary(m => m.Name.Trim(), m => m, StringComparer.OrdinalIgnoreCase);
        _modelsByMakeAndName = models.ToDictionary(
            m => (m.CarMakeId, m.Name.Trim().ToUpperInvariant()),
            m => m);
    }

    public static async Task<MakeModelResolver> LoadAsync(AppDbContext db, CancellationToken ct = default) =>
        new(db, await db.CarMakes.ToListAsync(ct), await db.CarModels.ToListAsync(ct));

    public CarMake GetOrCreateMake(string? name)
    {
        var trimmed = string.IsNullOrWhiteSpace(name) ? UnknownName : name.Trim();
        if (_makesByName.TryGetValue(trimmed, out var make))
        {
            return make;
        }

        make = new CarMake { Name = trimmed };
        _db.CarMakes.Add(make);
        _makesByName[trimmed] = make;
        return make;
    }

    public CarModel GetOrCreateModel(CarMake make, string? name)
    {
        var trimmed = string.IsNullOrWhiteSpace(name) ? UnknownName : name.Trim();
        var key = (make.Id, trimmed.ToUpperInvariant());
        if (_modelsByMakeAndName.TryGetValue(key, out var model))
        {
            return model;
        }

        model = new CarModel { Name = trimmed, CarMakeId = make.Id };
        _db.CarModels.Add(model);
        _modelsByMakeAndName[key] = model;
        return model;
    }
}
