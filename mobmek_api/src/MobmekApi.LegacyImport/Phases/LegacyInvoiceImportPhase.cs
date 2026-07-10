using MobmekApi.LegacyImport.Legacy;
using MobmekApi.LegacyImport.Mappers;
using MobmekApi.LegacyImport.Pipeline;

namespace MobmekApi.LegacyImport.Phases;

/// <summary>
/// Imports first-generation Invoices with their items (design §3.5). Each legacy invoice
/// pointed straight at a Car, so it gets a synthetic Job on that car; the job and items are
/// atomic with the invoice — only the invoice carries a map entry.
/// </summary>
public sealed class LegacyInvoiceImportPhase : ImportPhase
{
    public const string EntityType = "LegacyInvoice";

    public override string Name => "legacy-invoices";

    public override async Task RunAsync(ImportContext context, LegacyDbReader source, CancellationToken ct)
    {
        var stats = context.StatsFor(Name);
        var carsById = (await source.CarsAsync(ct)).ToDictionary(c => c.CarId);
        var itemsByInvoice = (await source.InvoiceItemsAsync(ct)).ToLookup(i => i.InvoiceId);

        foreach (var legacy in await source.InvoicesAsync(ct))
        {
            if (context.Map.Contains(EntityType, legacy.InvoiceId))
            {
                stats.Skipped++;
                continue;
            }

            var carId = context.Map.Get(CarImportPhase.EntityType, legacy.CarId);
            var customerId = context.Map.Get(CustomerImportPhase.EntityType, carsById[legacy.CarId].CustomerId);

            var job = SyntheticJobBuilder.Build(
                customerId, carId, legacy.IssueName, "Invoice", legacy.InvoiceId, legacy.DateAdded, legacy.DateEdited);
            context.Db.Jobs.Add(job);

            var invoice = DocumentMapper.Map(legacy, job.Id);
            context.Db.Invoices.Add(invoice);

            foreach (var item in itemsByInvoice[legacy.InvoiceId])
            {
                DocumentItemImport.Add(
                    context, invoice, $"InvoiceItem #{item.InvoiceItemId}",
                    item.ItemName, item.Quantity, item.ItemPrice, item.ItemTotal);
            }

            await context.Map.AddAsync(EntityType, legacy.InvoiceId, invoice.Id, ct);
            stats.Imported++;
        }
    }
}
