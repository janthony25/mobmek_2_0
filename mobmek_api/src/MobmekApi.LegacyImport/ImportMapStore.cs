using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MobmekApi.Data;

namespace MobmekApi.LegacyImport;

/// <summary>
/// The importer's own idempotency ledger: <c>legacy_import_map</c> records (entity type,
/// legacy int id) → new Guid for every migrated row, so re-runs skip already-imported rows
/// (design §1.3). Raw DDL/SQL on the app's connection — deliberately NOT part of
/// <see cref="AppDbContext"/>'s model; the table is dropped after migration sign-off.
/// All writes go through the context's current connection, so they join the ambient
/// phase/dry-run transaction and roll back with it.
/// </summary>
public sealed class ImportMapStore(AppDbContext db)
{
    private readonly Dictionary<(string EntityType, int LegacyId), Guid> _map = [];

    public int Count => _map.Count;

    public async Task EnsureTableAsync(CancellationToken ct = default) =>
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS legacy_import_map (
                entity_type     text        NOT NULL,
                legacy_id       integer     NOT NULL,
                new_id          uuid        NOT NULL,
                imported_at_utc timestamptz NOT NULL DEFAULT now(),
                PRIMARY KEY (entity_type, legacy_id)
            )
            """,
            ct);

    /// <summary>Loads all existing mappings into memory. Call once after <see cref="EnsureTableAsync"/>.</summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        _map.Clear();
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT entity_type, legacy_id, new_id FROM legacy_import_map";
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            _map[(reader.GetString(0), reader.GetInt32(1))] = reader.GetGuid(2);
        }
    }

    public bool Contains(string entityType, int legacyId) => _map.ContainsKey((entityType, legacyId));

    /// <summary>All new-system ids recorded for one entity type (reconciliation scopes to these).</summary>
    public IReadOnlyList<Guid> NewIdsFor(string entityType) =>
        [.. _map.Where(kv => kv.Key.EntityType == entityType).Select(kv => kv.Value)];

    public Guid? TryGet(string entityType, int legacyId) =>
        _map.TryGetValue((entityType, legacyId), out var id) ? id : null;

    /// <summary>Lookup that must succeed — a miss means a phase ran out of order or source data is orphaned.</summary>
    public Guid Get(string entityType, int legacyId) =>
        TryGet(entityType, legacyId)
        ?? throw new InvalidOperationException($"No import mapping for {entityType} #{legacyId} — phase order or orphaned legacy row?");

    /// <summary>Records a migrated row (memory + table, inside the ambient transaction).</summary>
    public async Task AddAsync(string entityType, int legacyId, Guid newId, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO legacy_import_map (entity_type, legacy_id, new_id) VALUES ({entityType}, {legacyId}, {newId})",
            ct);
        _map[(entityType, legacyId)] = newId;
    }
}
