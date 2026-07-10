using System.Text;

namespace MobmekApi.LegacyImport.Report;

/// <summary>
/// Builds the markdown import report (design §6): run info, source counts, per-phase
/// counters, and flags grouped by category (the owner's manual-cleanup worklist).
/// Reconciliation totals are appended by the document phases once they exist (Phase 4+).
/// </summary>
public static class ImportReportWriter
{
    public static string Build(
        bool dryRun,
        DateTime startedUtc,
        IReadOnlyDictionary<string, int> sourceCounts,
        ImportContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Legacy Import Report");
        sb.AppendLine();
        sb.AppendLine($"- **Run:** {startedUtc:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"- **Mode:** {(dryRun ? "DRY-RUN (all changes rolled back)" : "REAL (committed)")}");
        sb.AppendLine($"- **Existing mappings at start:** {context.Map.Count}");
        sb.AppendLine();

        sb.AppendLine("## Source row counts (legacy MSSQL)");
        sb.AppendLine();
        sb.AppendLine("| Table | Rows |");
        sb.AppendLine("|---|---|");
        foreach (var (table, count) in sourceCounts)
        {
            sb.AppendLine($"| {table} | {count} |");
        }

        sb.AppendLine();

        sb.AppendLine("## Phases");
        sb.AppendLine();
        if (context.Stats.Count == 0)
        {
            sb.AppendLine("_No phases ran._");
        }
        else
        {
            sb.AppendLine("| Phase | Imported | Skipped (already mapped) | Failed |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var (phase, stats) in context.Stats)
            {
                sb.AppendLine($"| {phase} | {stats.Imported} | {stats.Skipped} | {stats.Failed} |");
            }
        }

        sb.AppendLine();

        sb.AppendLine("## Reconciliation (§6 — every row must match for sign-off)");
        sb.AppendLine();
        if (context.Reconciliation.Count == 0)
        {
            sb.AppendLine("_Not computed (reconcile phase did not run)._");
        }
        else
        {
            sb.AppendLine("| Metric | Legacy | Imported | Match |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var row in context.Reconciliation)
            {
                sb.AppendLine($"| {row.Metric} | {row.Legacy} | {row.Imported} | {(row.Match ? "✅" : "❌")} |");
            }
        }

        sb.AppendLine();

        sb.AppendLine("## Flags (manual-cleanup worklist)");
        sb.AppendLine();
        if (context.Flags.Count == 0)
        {
            sb.AppendLine("_None._");
        }
        else
        {
            foreach (var group in context.Flags.GroupBy(f => f.Category))
            {
                sb.AppendLine($"### {group.Key} ({group.Count()})");
                sb.AppendLine();
                foreach (var flag in group)
                {
                    sb.AppendLine($"- `{flag.LegacyRef}` — {flag.Message}");
                }

                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
