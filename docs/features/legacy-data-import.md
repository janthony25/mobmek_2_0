# Legacy Data Import (old MSSQL → Postgres)

**Last verified:** 2026-10-11 (ran the real Phase 6 cutover this session — dry-run + real run against `legacy-backup/db_aae44c_mobmekv200_10_11_2026_0.bak`, read the resulting `legacy-import-report-20261011-080122.md`, queried both local and EC2 Postgres row counts directly)

## Status summary

| Sub-capability | Status |
|---|---|
| Importer mechanism / idempotency | Working, verified live |
| Phases 0-5 (lookups → customers → cars → jobs → documents → appointments) | Done |
| Phase 6 cutover (prod wipe, fresh-backup import, data shipped to EC2) | **Done 2026-10-11** |
| Design/todo/testing docs | Stale — frozen since 2026-07-10, not rewritten this session either |

## Details

### Mechanism — one-off CLI, not an API/UI feature
Lives entirely in `mobmek_api/src/MobmekApi.LegacyImport/` (19 files: `Program.cs`, `Pipeline/`, `Phases/` ×11, `Mappers/` ×8, `Legacy/`, `ImportMapStore.cs`, `Report/ImportReportWriter.cs`), referencing `MobmekApi.csproj` directly and writing through the real `AppDbContext`. Run via `dotnet run --project src/MobmekApi.LegacyImport -- [--dry-run]`.

**Idempotency is real, not just claimed** — confirmed by directly querying the live dev Postgres `legacy_import_map` table: 2428 rows total (Appointment 262, Car 482, Customer 463, Job 449, LegacyInvoice 105, LegacyQuotation 123, NewInvoice 480, NewQuotation 58, Service 6), matching the most recent report's header exactly.

Import order matches the design doc: Lookups → Customers → Cars → Jobs+children → Documents (legacy Invoice/Quotation via synthetic jobs, then NewInvoice/NewQuotation) → Appointments. Mechanics, Reminders, and Identity users are explicitly out of scope.

### Drift: an undocumented second real run happened after the docs were written
`legacy-import-report-20260808-014847.md` is a **real (non-dry-run)** import dated 2026-08-08, against a *newer* source backup (`legacy-backup/db_aae44c_mobmekv200_8_7_2026_18.bak`, confirmed a real MSSQL `.bak` via `file`) than the one the design/testing docs reference by name. Reconciliation is all-✅ (18/18 rows). **None of the three docs (`design`/`todo`/`testing`) have been updated since** — `git log` shows exactly one commit ever touching them (`d94a98d Add legacy support`). `legacy-import-testing.md` still cites the 2026-07-10 report as "current," a full generation of data behind.

This run was additive (no DB reset) — the live DB currently has a few more dev-native rows than mapped-legacy rows (e.g. 485 Cars vs. 482 mapped), exactly matching the pre-cutover state the testing doc describes.

### Phase 6 cutover — done 2026-10-11
Ran end-to-end this session against a fresh cutover `.bak` (`db_aae44c_mobmekv200_10_11_2026_0.bak`, dated the same day):

1. **Wiped EC2 production's dummy workshop data** — `TRUNCATE "Customers", "CarMakes", "CarModels" CASCADE` over SSH, which cascaded to `Cars`, `Jobs` (+children), `Invoices`/`InvoiceItems`, `Appointments`, `Notes`, `Reminders`, `OutboundEmails`, `CashTransactions`/`TransactionAttachments`. Confirmed untouched: `AspNetUsers`, `BusinessDetails`, `EmailTemplates`/`EmailSettings`, `GstSettings`, `Employees`, schema/migrations.
2. **Reset the local dev DB and `legacy_import_map`**, removed the stale `mobmek_legacy_mssql` container (anonymous volume, forces a fresh restore), replaced `legacy-backup/*.bak` with the new file (old one archived to `legacy-backup/archive/`).
3. **Found and fixed a real bug**: the plain unique index added 2026-10-06 (`AddInvoiceSequenceNumberUniqueIndex`) broke the importer's "insert at `SequenceNumber = 0`, number for real afterwards" design — multiple same-`DocumentType` zeros in one `SaveChanges` batch violated it. Fixed via a new migration (`MakeInvoiceSequenceNumberIndexPartial`) making the index partial (`WHERE "SequenceNumber" > 0`), which preserves the real-concurrency protection while letting the importer's placeholder zeros coexist. Applied to EC2 via the documented `generate-migration-script.sh` → `psql` process (pre-migrate `pg_dump` safety snapshot taken first).
4. **Dry-run then real run** locally: 18/18 reconciliation rows ✅ — 510 customers, 530 cars, 759 jobs (531 real + 228 synthetic), 863 documents, 1866 item lines, 269 appointments, 32 makes / 212 models.
5. **Shipped the data to EC2** via `pg_dump --data-only --disable-triggers` (only the tables the import populated: `CarMakes`/`CarModels`/`Customers`/`Cars`/`JobServices`/`Jobs`/`JobItems`/`Labour`/`JobServiceLines`/`Invoices`/`InvoiceItems`/`Appointments`) → `scp` → `psql` restore — never ran SQL Server or the importer directly on the production box. Row counts verified identical between local and EC2 afterward.

**Flagged, not auto-resolved** (by design — owner reviews manually): 37 suspected-duplicate customers (name/phone match), 6 duplicate-rego cars, 22 placeholder phones, 206 single-word names, 1 truncated item name, 24 appointment end-times adjusted (start+1h) — full detail in `mobmek_api/legacy-import-report-20261011-080122.md`.

**Not done as part of this cutover**: `legacy_import_map` table and the `legacy-mssql` compose profile/container were left in place (not torn down) — `docker-compose.yml` still has `legacy-mssql` behind the `legacy` profile pending a deliberate cleanup pass. `docs/legacy-import-{design,todo,testing}.md` were not rewritten.

### Manual duplicate-customer merges (owner-driven, in progress)
Tracked in `docs/legacy-import-duplicate-customers.md`. 3 of 37 flagged pairs merged directly on EC2 production so far (2026-10-11), each as a single transaction: re-point the duplicate's `Cars`/`Jobs`/`Appointments` to the surviving `Customer.Id`, then delete the duplicate row. A `pg_dump` safety snapshot was taken on the box first (this is real customer data, not the dummy rows wiped earlier). Done: Joseph Gilliard (`#2468`/`#2469`), Aaron (`#2159`/`#2278`), Jhovan/Jovan (`#2372`/`#2376`, kept JHOVAN — verified it's the more *active* record: a job from 2026-08-22 vs. the other's latest at 2026-03-16, even though its account was created earlier). These were raw SQL against EC2 only — **not** mirrored to the local dev DB, and not yet applied to local.

### Test coverage for the sequence-number index fix
`SequenceNumberAssignerTests.cs` only tested the pure numbering math in memory — it never exercised the actual `SaveChanges` insert path that hit the real unique-index constraint, so it could not have caught (and still can't protect against regressing) the `AddInvoiceSequenceNumberUniqueIndex` bug above. Checked: **100% of `MobmekApi.Tests` uses EF Core's InMemory provider**, which silently ignores `HasFilter` partial indexes — a structural blind spot for this whole class of bug. Added `tests/MobmekApi.Tests/Data/InvoiceSequenceIndexTests.cs` (2026-10-11), the first test in the suite to connect to a real Postgres instead: asserts two `SequenceNumber = 0` placeholders of the same `DocumentType` don't collide, and that two real (`>0`) numbers of the same type still do. Needs `docker compose up -d db` running locally to pass; everything runs inside a transaction that's always rolled back, so no test data is left in the dev DB. 647/647 tests green including these two.

## Recommended follow-ups
- Manually review the remaining 34 of 37 suspected-duplicate customers and all 6 duplicate-rego pairs in the new UI (owner task — the report is the worklist).
- Decide whether to mirror the 3 EC2 customer merges above into the local dev DB, for consistency with future testing.
- Update `docs/legacy-import-{design,todo,testing}.md` to reflect this 2026-10-11 run as the real cutover (they're still frozen at 2026-07-10 content-wise).
- Tear down `legacy_import_map` and remove `legacy-mssql` from `docker-compose.yml` + delete the importer project once the flagged data has been cleaned up and nobody needs to re-run the import.
- `car-make-model.md` (new, more exhaustive NZ make/model reference) still needs to be merged into the `CarMakes`/`CarModels` tables — not done this session; needs a find-or-create seeder (same case-insensitive matching as `MakeModelResolver`) so it doesn't duplicate the 32 makes/212 models the legacy import just created.
- Run the Phase 6 manual sign-off checklist (`docs/legacy-import-todo.md`) against the real EC2 data — open a legacy customer end-to-end, check an invoice total against an old printout, edit an imported customer, add a new car/job/invoice to one. Not yet done.
