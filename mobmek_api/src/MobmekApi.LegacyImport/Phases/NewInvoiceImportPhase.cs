using MobmekApi.Entities;
using MobmekApi.LegacyImport.Legacy;
using MobmekApi.LegacyImport.Mappers;
using MobmekApi.LegacyImport.Pipeline;

namespace MobmekApi.LegacyImport.Phases;

/// <summary>
/// Imports second-generation NewInvoices with their items (design §3.5), attached to the
/// jobs the jobs phase mapped. Payment fields (DatePaid, ModeOfPayment, Cash/CardAmount)
/// carry over; an Active invoice bumps its job to Invoiced, mirroring what generating an
/// invoice does in the new system (normally a no-op — the jobs phase already applied it).
/// </summary>
public sealed class NewInvoiceImportPhase : ImportPhase
{
    public const string EntityType = "NewInvoice";

    public override string Name => "new-invoices";

    public override async Task RunAsync(ImportContext context, LegacyDbReader source, CancellationToken ct)
    {
        var stats = context.StatsFor(Name);
        var itemsByInvoice = (await source.NewInvoiceItemsAsync(ct)).ToLookup(i => i.NewInvoiceId);

        foreach (var legacy in await source.NewInvoicesAsync(ct))
        {
            if (context.Map.Contains(EntityType, legacy.NewInvoiceId))
            {
                stats.Skipped++;
                continue;
            }

            var jobId = context.Map.Get(JobImportPhase.EntityType, legacy.JobId);
            var invoice = DocumentMapper.Map(legacy, jobId);
            context.Db.Invoices.Add(invoice);

            foreach (var item in itemsByInvoice[legacy.NewInvoiceId])
            {
                DocumentItemImport.Add(
                    context, invoice, $"NewInvoiceItem #{item.NewInvoiceItemId}",
                    item.ItemName, item.Quantity, item.ItemPrice, item.ItemTotal);
            }

            if (legacy.Status.Trim().Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                var job = await context.Db.Jobs.FindAsync([jobId], ct);
                if (job is not null && job.Status != JobStatus.Invoiced)
                {
                    job.Status = JobStatus.Invoiced;
                }
            }

            await context.Map.AddAsync(EntityType, legacy.NewInvoiceId, invoice.Id, ct);
            stats.Imported++;
        }
    }
}
