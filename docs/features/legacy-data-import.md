# Legacy Data Import (old MSSQL → Postgres)

**Last verified:** 2026-10-07 (read `mobmek_api/src/MobmekApi.LegacyImport/*`, `docs/legacy-import-{design,todo,testing}.md`, `legacy-import-report-*.md`, `legacy-backup/`, and queried the live dev Postgres `legacy_import_map` table directly)

## Status summary

| Sub-capability | Status |
|---|---|
| Importer mechanism / idempotency | Working, verified live |
| Phases 0-5 (lookups → customers → cars → jobs → documents → appointments) | Done |
| Design/todo/testing docs | **Stale** — frozen since 2026-07-10 |
| Phase 6 cutover (final wipe, sign-off, teardown) | Not done |

## Details

### Mechanism — one-off CLI, not an API/UI feature
Lives entirely in `mobmek_api/src/MobmekApi.LegacyImport/` (19 files: `Program.cs`, `Pipeline/`, `Phases/` ×11, `Mappers/` ×8, `Legacy/`, `ImportMapStore.cs`, `Report/ImportReportWriter.cs`), referencing `MobmekApi.csproj` directly and writing through the real `AppDbContext`. Run via `dotnet run --project src/MobmekApi.LegacyImport -- [--dry-run]`.

**Idempotency is real, not just claimed** — confirmed by directly querying the live dev Postgres `legacy_import_map` table: 2428 rows total (Appointment 262, Car 482, Customer 463, Job 449, LegacyInvoice 105, LegacyQuotation 123, NewInvoice 480, NewQuotation 58, Service 6), matching the most recent report's header exactly.

Import order matches the design doc: Lookups → Customers → Cars → Jobs+children → Documents (legacy Invoice/Quotation via synthetic jobs, then NewInvoice/NewQuotation) → Appointments. Mechanics, Reminders, and Identity users are explicitly out of scope.

### Drift: an undocumented second real run happened after the docs were written
`legacy-import-report-20260808-014847.md` is a **real (non-dry-run)** import dated 2026-08-08, against a *newer* source backup (`legacy-backup/db_aae44c_mobmekv200_8_7_2026_18.bak`, confirmed a real MSSQL `.bak` via `file`) than the one the design/testing docs reference by name. Reconciliation is all-✅ (18/18 rows). **None of the three docs (`design`/`todo`/`testing`) have been updated since** — `git log` shows exactly one commit ever touching them (`d94a98d Add legacy support`). `legacy-import-testing.md` still cites the 2026-07-10 report as "current," a full generation of data behind.

This run was additive (no DB reset) — the live DB currently has a few more dev-native rows than mapped-legacy rows (e.g. 485 Cars vs. 482 mapped), exactly matching the pre-cutover state the testing doc describes.

### Phase 6 cutover — not done
No dev-data wipe, no final sign-off checklist run, `legacy_import_map` table still present (2428 rows), and the `mobmek_legacy_mssql` container still exists (stopped/`Exited (255)`, not removed). Memory's "Phases 0-5 done, Phase 6 owner-gated" is directionally correct but was missing the fact of the Aug 8 run.

## Recommended follow-ups
- Update `docs/legacy-import-{design,todo,testing}.md` to reflect the 2026-08-08 run and the newer source backup.
- When ready for Phase 6: run the formal cutover (fresh backup, dev-data wipe, final reconciliation sign-off), then tear down `legacy_import_map` and remove `legacy-mssql` from `docker-compose.yml`.
