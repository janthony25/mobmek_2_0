using MobmekApi.LegacyImport.Legacy;

namespace MobmekApi.LegacyImport.Pipeline;

/// <summary>
/// One step of the import (design §2). Phases run in registration order; each phase's
/// writes are committed atomically by the pipeline (or rolled back wholesale in dry-run).
/// Implementations must skip rows already present in <see cref="ImportContext.Map"/> so
/// re-runs are safe.
/// </summary>
public abstract class ImportPhase
{
    /// <summary>Stable name used for `--phase` selection and report sections.</summary>
    public abstract string Name { get; }

    public abstract Task RunAsync(ImportContext context, LegacyDbReader source, CancellationToken ct);
}
