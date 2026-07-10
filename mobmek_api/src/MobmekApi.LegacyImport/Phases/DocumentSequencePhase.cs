using Microsoft.EntityFrameworkCore;
using MobmekApi.LegacyImport.Legacy;
using MobmekApi.LegacyImport.Mappers;
using MobmekApi.LegacyImport.Pipeline;

namespace MobmekApi.LegacyImport.Phases;

/// <summary>
/// Assigns printed sequence numbers to the documents the four import phases inserted with
/// SequenceNumber 0 (design §3.5) — the app itself always assigns ≥ 1, so zero uniquely
/// marks unnumbered imports. Updates go through raw SQL so SaveChanges doesn't re-stamp
/// the imported UpdatedAtUtc audit dates. Idempotent: a re-run finds nothing at zero.
/// </summary>
public sealed class DocumentSequencePhase : ImportPhase
{
    public override string Name => "document-sequences";

    public override async Task RunAsync(ImportContext context, LegacyDbReader source, CancellationToken ct)
    {
        var stats = context.StatsFor(Name);

        var unnumbered = await context.Db.Invoices.AsNoTracking()
            .Where(i => i.SequenceNumber == 0)
            .ToListAsync(ct);

        var assignments = SequenceNumberAssigner.Assign(unnumbered, type =>
            context.Db.Invoices
                .Where(i => i.DocumentType == type && i.SequenceNumber > 0)
                .Max(i => (int?)i.SequenceNumber) ?? 0);

        foreach (var (invoice, number) in assignments)
        {
            await context.Db.Database.ExecuteSqlAsync(
                $"""UPDATE "Invoices" SET "SequenceNumber" = {number} WHERE "Id" = {invoice.Id}""",
                ct);
            stats.Imported++;
        }
    }
}
