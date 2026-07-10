using MobmekApi.LegacyImport.Legacy;
using MobmekApi.LegacyImport.Mappers;
using MobmekApi.LegacyImport.Pipeline;

namespace MobmekApi.LegacyImport.Phases;

/// <summary>
/// Imports first-generation Quotations with their items (design §3.5). Like legacy
/// invoices, each one pointed straight at a Car and gets a synthetic Job (status Completed).
/// </summary>
public sealed class LegacyQuotationImportPhase : ImportPhase
{
    public const string EntityType = "LegacyQuotation";

    public override string Name => "legacy-quotations";

    public override async Task RunAsync(ImportContext context, LegacyDbReader source, CancellationToken ct)
    {
        var stats = context.StatsFor(Name);
        var carsById = (await source.CarsAsync(ct)).ToDictionary(c => c.CarId);
        var itemsByQuotation = (await source.QuotationItemsAsync(ct)).ToLookup(i => i.QuotationId);

        foreach (var legacy in await source.QuotationsAsync(ct))
        {
            if (context.Map.Contains(EntityType, legacy.QuotationId))
            {
                stats.Skipped++;
                continue;
            }

            var carId = context.Map.Get(CarImportPhase.EntityType, legacy.CarId);
            var customerId = context.Map.Get(CustomerImportPhase.EntityType, carsById[legacy.CarId].CustomerId);

            var job = SyntheticJobBuilder.Build(
                customerId, carId, legacy.IssueName, "Quotation", legacy.QuotationId, legacy.DateAdded, legacy.DateEdited);
            context.Db.Jobs.Add(job);

            var invoice = DocumentMapper.Map(legacy, job.Id);
            context.Db.Invoices.Add(invoice);

            foreach (var item in itemsByQuotation[legacy.QuotationId])
            {
                DocumentItemImport.Add(
                    context, invoice, $"QuotationItem #{item.QuotationItemId}",
                    item.ItemName, item.Quantity, item.ItemPrice, item.ItemTotal);
            }

            await context.Map.AddAsync(EntityType, legacy.QuotationId, invoice.Id, ct);
            stats.Imported++;
        }
    }
}
