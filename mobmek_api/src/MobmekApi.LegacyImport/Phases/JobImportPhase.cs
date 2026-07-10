using MobmekApi.LegacyImport.Legacy;
using MobmekApi.LegacyImport.Mappers;
using MobmekApi.LegacyImport.Pipeline;

namespace MobmekApi.LegacyImport.Phases;

/// <summary>
/// Imports legacy Jobs with their JobItems, Labour and JobServiceLines in one pass
/// (design §3.4). Runs after customers, cars and the service catalog. The owning customer
/// is derived through the legacy car; an Active NewInvoice forces status Invoiced.
/// Children carry no map entries of their own — they are atomic with their job.
/// </summary>
public sealed class JobImportPhase : ImportPhase
{
    public const string EntityType = "Job";

    public override string Name => "jobs";

    public override async Task RunAsync(ImportContext context, LegacyDbReader source, CancellationToken ct)
    {
        var stats = context.StatsFor(Name);

        var carsById = (await source.CarsAsync(ct)).ToDictionary(c => c.CarId);
        var mechanicsById = (await source.MechanicsAsync(ct)).ToDictionary(m => m.MechanicId);
        var servicesById = (await source.ServicesAsync(ct)).ToDictionary(s => s.ServiceId);
        var itemsByJob = (await source.JobItemsAsync(ct)).ToLookup(i => i.JobId);
        var laboursByJob = (await source.LaboursAsync(ct)).ToLookup(l => l.JobId);
        var serviceJoinsByJob = (await source.JobServiceJoinsAsync(ct)).ToLookup(j => j.JobId);
        var activeNewInvoiceJobIds = (await source.NewInvoicesAsync(ct))
            .Where(i => i.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            .Select(i => i.JobId)
            .ToHashSet();

        foreach (var legacy in await source.JobsAsync(ct))
        {
            if (context.Map.Contains(EntityType, legacy.JobId))
            {
                stats.Skipped++;
                continue;
            }

            var jobRef = $"Job #{legacy.JobId}";
            var legacyCar = carsById[legacy.CarId];
            var carId = context.Map.Get(CarImportPhase.EntityType, legacy.CarId);
            var customerId = context.Map.Get(CustomerImportPhase.EntityType, legacyCar.CustomerId);

            var labours = laboursByJob[legacy.JobId].ToList();
            var mechanicName = legacy.MechanicId is int mechanicId && mechanicsById.TryGetValue(mechanicId, out var mechanic)
                ? mechanic.MechanicName
                : null;

            var (job, statusFlagRaw) = JobMapper.Map(
                legacy,
                customerId,
                carId,
                [.. labours.Select(l => l.LabourName)],
                mechanicName,
                activeNewInvoiceJobIds.Contains(legacy.JobId));

            if (statusFlagRaw is not null)
            {
                context.Flag(
                    "job-status-mapped",
                    jobRef,
                    $"Legacy status '{statusFlagRaw}' has no direct equivalent — imported as '{job.Status}'");
            }

            context.Db.Jobs.Add(job);

            foreach (var item in itemsByJob[legacy.JobId])
            {
                context.Db.JobItems.Add(JobItemMapper.Map(item, job.Id));
            }

            foreach (var labour in labours)
            {
                context.Db.Labour.Add(LabourMapper.Map(labour, job.Id));
            }

            foreach (var join in serviceJoinsByJob[legacy.JobId])
            {
                var jobServiceId = context.Map.Get(ServiceCatalogImportPhase.EntityType, join.ServiceId);
                context.Db.JobServiceLines.Add(ServiceLineMapper.Map(join, job.Id, jobServiceId, servicesById[join.ServiceId].Price));
            }

            await context.Map.AddAsync(EntityType, legacy.JobId, job.Id, ct);
            stats.Imported++;
        }
    }
}
