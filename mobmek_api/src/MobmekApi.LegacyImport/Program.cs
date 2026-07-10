using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MobmekApi.Data;
using MobmekApi.LegacyImport;
using MobmekApi.LegacyImport.Legacy;
using MobmekApi.LegacyImport.Phases;
using MobmekApi.LegacyImport.Pipeline;
using MobmekApi.LegacyImport.Report;

// One-time legacy data importer (docs/legacy-import-design.md).
// Usage: dotnet run --project src/MobmekApi.LegacyImport -- [--dry-run] [--phase <name>]

var dryRun = args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);
string? onlyPhase = null;
var phaseFlagIndex = Array.FindIndex(args, a => a.Equals("--phase", StringComparison.OrdinalIgnoreCase));
if (phaseFlagIndex >= 0)
{
    if (phaseFlagIndex + 1 >= args.Length)
    {
        Console.Error.WriteLine("--phase requires a phase name");
        return 1;
    }

    onlyPhase = args[phaseFlagIndex + 1];
}

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.local.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var legacyConnection = configuration.GetConnectionString("LegacyDb")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:LegacyDb");
var targetConnection = configuration.GetConnectionString("TargetDb")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:TargetDb");

var startedUtc = DateTime.UtcNow;
Console.WriteLine($"Legacy import — {(dryRun ? "DRY-RUN" : "REAL RUN")}{(onlyPhase is null ? "" : $", phase: {onlyPhase}")}");

// Source: restored legacy MSSQL (read-only usage).
await using var source = new LegacyDbReader(legacyConnection);
Console.WriteLine("Connecting to legacy MSSQL…");
var sourceCounts = await source.CountAllAsync();
Console.WriteLine($"  OK — {sourceCounts.Count} tables, {sourceCounts.Values.Sum()} rows total");

// Target: the real AppDbContext so every EF mapping and constraint applies (§1.2).
var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(targetConnection)
    .Options;
await using var db = new AppDbContext(options);
Console.WriteLine("Connecting to target Postgres…");
if (!await db.Database.CanConnectAsync())
{
    Console.Error.WriteLine("  Cannot connect to the target database — is `docker compose up -d db` running?");
    return 1;
}

Console.WriteLine("  OK");

var map = new ImportMapStore(db);
var context = new ImportContext(db, map, dryRun);

// Phase registration, in dependency order (§2); reconcile always runs last.
var pipeline = new ImportPipeline(
[
    new ServiceCatalogImportPhase(),
    new CustomerImportPhase(),
    new CarImportPhase(),
    new JobImportPhase(),
    new LegacyInvoiceImportPhase(),
    new LegacyQuotationImportPhase(),
    new NewInvoiceImportPhase(),
    new NewQuotationImportPhase(),
    new DocumentSequencePhase(),
    new AppointmentImportPhase(),
    new ReconciliationPhase(),
]);

try
{
    if (dryRun)
    {
        // Single transaction around everything (map DDL included) — rolled back at the end.
        await using var transaction = await db.Database.BeginTransactionAsync();
        await map.EnsureTableAsync();
        await map.LoadAsync();
        await pipeline.RunAsync(context, source, onlyPhase);
        await transaction.RollbackAsync();
        Console.WriteLine("Dry-run finished — all changes rolled back.");
    }
    else
    {
        await map.EnsureTableAsync();
        await map.LoadAsync();
        await pipeline.RunAsync(context, source, onlyPhase);
        Console.WriteLine("Import finished.");
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Import FAILED: {ex}");
    return 1;
}
finally
{
    var report = ImportReportWriter.Build(dryRun, startedUtc, sourceCounts, context);
    var reportPath = Path.GetFullPath($"legacy-import-report-{startedUtc:yyyyMMdd-HHmmss}.md");
    await File.WriteAllTextAsync(reportPath, report);
    Console.WriteLine($"Report: {reportPath}");
}

return 0;
