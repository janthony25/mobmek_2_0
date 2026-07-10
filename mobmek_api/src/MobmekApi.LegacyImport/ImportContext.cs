using MobmekApi.Data;

namespace MobmekApi.LegacyImport;

/// <summary>A data-quality note for the import report's manual-cleanup worklist (design §6).</summary>
public sealed record ImportFlag(string Category, string LegacyRef, string Message);

/// <summary>One legacy-vs-imported comparison in the report's reconciliation table (design §6).</summary>
public sealed record ReconciliationRow(string Metric, string Legacy, string Imported, bool Match);

/// <summary>Per-phase outcome counters for the report.</summary>
public sealed class PhaseStats
{
    public int Imported { get; set; }

    public int Skipped { get; set; }

    public int Failed { get; set; }
}

/// <summary>
/// Everything a phase needs while running: the target context, the idempotency map,
/// the dry-run switch, and the report accumulators (flags + counters).
/// </summary>
public sealed class ImportContext(AppDbContext db, ImportMapStore map, bool dryRun)
{
    public AppDbContext Db { get; } = db;

    public ImportMapStore Map { get; } = map;

    public bool DryRun { get; } = dryRun;

    public List<ImportFlag> Flags { get; } = [];

    /// <summary>Filled by the reconcile phase; every row must match for sign-off (§6).</summary>
    public List<ReconciliationRow> Reconciliation { get; } = [];

    /// <summary>Phase name → counters, in registration order.</summary>
    public Dictionary<string, PhaseStats> Stats { get; } = [];

    public PhaseStats StatsFor(string phaseName)
    {
        if (!Stats.TryGetValue(phaseName, out var stats))
        {
            stats = new PhaseStats();
            Stats[phaseName] = stats;
        }

        return stats;
    }

    public void Flag(string category, string legacyRef, string message) =>
        Flags.Add(new ImportFlag(category, legacyRef, message));
}
