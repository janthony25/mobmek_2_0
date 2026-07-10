using Microsoft.EntityFrameworkCore;
using MobmekApi.Data;
using MobmekApi.Entities;
using MobmekApi.LegacyImport.Legacy;

namespace MobmekApi.LegacyImport.Mappers;

/// <summary>
/// Find-or-create for the JobService catalog (design §3.3). Case-insensitive name match so
/// legacy services ("Oil Change") reuse pre-seeded catalog entries instead of duplicating them.
/// </summary>
public sealed class ServiceCatalogResolver
{
    private readonly AppDbContext _db;
    private readonly Dictionary<string, JobService> _byName;

    private ServiceCatalogResolver(AppDbContext db, List<JobService> existing)
    {
        _db = db;
        _byName = existing.ToDictionary(s => s.Name.Trim(), s => s, StringComparer.OrdinalIgnoreCase);
    }

    public static async Task<ServiceCatalogResolver> LoadAsync(AppDbContext db, CancellationToken ct = default) =>
        new(db, await db.JobServices.ToListAsync(ct));

    /// <summary>Returns the matching catalog entry, or creates one from the legacy service.</summary>
    public (JobService Service, bool ReusedExisting) GetOrCreate(LegacyService legacy)
    {
        var name = legacy.Name.Trim();
        if (_byName.TryGetValue(name, out var existing))
        {
            return (existing, true);
        }

        var service = new JobService
        {
            Name = name,
            Description = string.IsNullOrWhiteSpace(legacy.Description) ? null : legacy.Description.Trim(),
            Price = legacy.Price,
            IsActive = legacy.IsActive,
            CreatedAtUtc = NzTime.ToUtc(legacy.DateCreated),
            UpdatedAtUtc = NzTime.ToUtc(legacy.DateUpdated),
        };
        _db.JobServices.Add(service);
        _byName[name] = service;
        return (service, false);
    }
}
