using MobmekApi.LegacyImport.Legacy;
using MobmekApi.LegacyImport.Mappers;
using MobmekApi.LegacyImport.Pipeline;

namespace MobmekApi.LegacyImport.Phases;

/// <summary>
/// Imports the legacy Service catalog into JobServices (design §3.3). Find-or-create by
/// name, so seeded catalog entries are reused; either way the legacy id is mapped so job
/// service lines can resolve it.
/// </summary>
public sealed class ServiceCatalogImportPhase : ImportPhase
{
    public const string EntityType = "Service";

    public override string Name => "services";

    public override async Task RunAsync(ImportContext context, LegacyDbReader source, CancellationToken ct)
    {
        var stats = context.StatsFor(Name);
        var resolver = await ServiceCatalogResolver.LoadAsync(context.Db, ct);

        foreach (var legacy in await source.ServicesAsync(ct))
        {
            if (context.Map.Contains(EntityType, legacy.ServiceId))
            {
                stats.Skipped++;
                continue;
            }

            var (service, reusedExisting) = resolver.GetOrCreate(legacy);
            await context.Map.AddAsync(EntityType, legacy.ServiceId, service.Id, ct);
            if (reusedExisting)
            {
                context.Flag(
                    "service-reused",
                    $"Service #{legacy.ServiceId}",
                    $"'{legacy.Name.Trim()}' matched an existing catalog entry — legacy price {legacy.Price:0.00} vs catalog {service.Price:0.00}");
            }

            stats.Imported++;
        }
    }
}
