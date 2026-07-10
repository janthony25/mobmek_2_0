using MobmekApi.LegacyImport.Legacy;
using MobmekApi.LegacyImport.Mappers;
using MobmekApi.LegacyImport.Pipeline;

namespace MobmekApi.LegacyImport.Phases;

/// <summary>
/// Imports second-generation NewQuotations with their items (design §3.5), attached to
/// mapped jobs. ValidUntil becomes DueDate; IsAccepted survives as a notes suffix.
/// </summary>
public sealed class NewQuotationImportPhase : ImportPhase
{
    public const string EntityType = "NewQuotation";

    public override string Name => "new-quotations";

    public override async Task RunAsync(ImportContext context, LegacyDbReader source, CancellationToken ct)
    {
        var stats = context.StatsFor(Name);
        var itemsByQuotation = (await source.NewQuotationItemsAsync(ct)).ToLookup(i => i.NewQuotationId);

        foreach (var legacy in await source.NewQuotationsAsync(ct))
        {
            if (context.Map.Contains(EntityType, legacy.NewQuotationId))
            {
                stats.Skipped++;
                continue;
            }

            var jobId = context.Map.Get(JobImportPhase.EntityType, legacy.JobId);
            var invoice = DocumentMapper.Map(legacy, jobId);
            context.Db.Invoices.Add(invoice);

            foreach (var item in itemsByQuotation[legacy.NewQuotationId])
            {
                DocumentItemImport.Add(
                    context, invoice, $"NewQuotationItem #{item.NewQuotationItemId}",
                    item.ItemName, item.Quantity, item.ItemPrice, item.ItemTotal);
            }

            await context.Map.AddAsync(EntityType, legacy.NewQuotationId, invoice.Id, ct);
            stats.Imported++;
        }
    }
}
