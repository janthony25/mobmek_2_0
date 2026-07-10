using MobmekApi.LegacyImport.Legacy;

namespace MobmekApi.LegacyImport.Pipeline;

/// <summary>
/// Runs the registered phases in order (design §2). Real run: one transaction per phase —
/// a phase either fully lands or not at all. Dry-run: the caller wraps the whole run in a
/// single transaction and rolls it back, so no per-phase transactions here (§1.3).
/// </summary>
public sealed class ImportPipeline(IReadOnlyList<ImportPhase> phases)
{
    public IReadOnlyList<ImportPhase> Phases => phases;

    public async Task RunAsync(ImportContext context, LegacyDbReader source, string? onlyPhase, CancellationToken ct = default)
    {
        var selected = onlyPhase is null
            ? phases
            : [.. phases.Where(p => p.Name.Equals(onlyPhase, StringComparison.OrdinalIgnoreCase))];
        if (onlyPhase is not null && selected.Count == 0)
        {
            var known = phases.Count == 0 ? "(none registered)" : string.Join(", ", phases.Select(p => p.Name));
            throw new ArgumentException($"Unknown phase '{onlyPhase}'. Available: {known}");
        }

        foreach (var phase in selected)
        {
            Console.WriteLine($"→ Phase: {phase.Name}");
            if (context.DryRun)
            {
                await phase.RunAsync(context, source, ct);
                await context.Db.SaveChangesAsync(ct);
            }
            else
            {
                await using var transaction = await context.Db.Database.BeginTransactionAsync(ct);
                await phase.RunAsync(context, source, ct);
                await context.Db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }

            var stats = context.StatsFor(phase.Name);
            Console.WriteLine($"  imported {stats.Imported}, skipped {stats.Skipped}, failed {stats.Failed}");
        }
    }
}
