# Legacy Data Import — Implementation Checklist (v1)

Tracks delivery of [`legacy-import-design.md`](./legacy-import-design.md). Section references (§) point into the design doc.

**Status:** Phases 0–5 complete 2026-07-10 — **all in-scope legacy data imported and reconciled into dev Postgres.** Next: Phase 6 cutover (needs owner: fresh `.bak`, target-DB reset, sign-off). Owner browser-testing walkthrough: [`legacy-import-testing.md`](./legacy-import-testing.md). Phases ship in order; each phase's import runs in its own transaction and is independently re-runnable (§1.3).

---

## Phase 0 — Restore the old database (operator + tooling, §1.1)

- [x] Owner requests a fresh `.bak` of the production MobileMekaniko database from the hosting provider (most hosts expose this via their control panel or on request) and saves it as `legacy-backup/MobileMekaniko.bak` at the repo root (folder is gitignored). A dev copy is fine to build against; a second fresh `.bak` is taken at final cutover (Phase 6) so nothing created in the old system meanwhile is missed *(provided 2026-07-10 as `db_aae44c_mobmekv200_7_9_2026_16.bak`)*
- [x] Add `legacy-mssql` service to `docker-compose.yml` under a `legacy` profile: `mcr.microsoft.com/mssql/server:2022-latest`, SA password via env, volume mounting `./legacy-backup`
- [x] Restore: `docker compose --profile legacy up -d` → `RESTORE DATABASE ... WITH MOVE` via `sqlcmd` (document exact commands in the compose file comments). Note: `azure-sql-edge` fallback did NOT work (the `.bak` is SQL 2022 / version 957; edge tops out at 931) — instead enabled `rosetta: true` in `~/.colima/default/colima.yaml` so `mssql/server:2022-latest` runs; restored as DB `MobileMekaniko`
- [x] Sanity queries: row counts recorded 2026-07-10 (reconciliation baseline, §6): Customers 447 · Cars 465 · Makes 33 · CarMakes 465 · Jobs 419 · JobItems 526 · Labours 177 · Services 6 · JobServices 188 · Invoices 105 · InvoiceItems 295 · Quotations 123 · QuotationItems 340 · NewInvoices 448 · NewInvoiceItems 745 · NewQuotations 49 · NewQuotationItems 147 · Appointments 259 · Mechanics 2 · Reminders 5
- [x] Enumerate real data values and **finalize the mapping tables in the design doc** (§3.4, §3.6, §8) — done 2026-07-10: Job statuses `Done/In Progress/Scheduled/Waiting Customer/Waiting for Parts`; appt statuses `Scheduled/In-Progress/In Progress/Done/Cancelled`; markup only `%`/`$`; every car has exactly 1 make + model + year (no Unknown fallbacks will fire); 6 dup regos; 20 null phones; 170 single-word names; 4 legacy invoices with null money fields; GST finding: legacy tax = 15% exclusive (see §3.5) — mapping tables updated in design doc
- [x] **Expected output:** old DB queryable on localhost (`localhost,1433`, DB `MobileMekaniko`, sa / `MSSQL_SA_PASSWORD` env, default `LegacyImport!2026`); baseline counts + distinct values recorded above — **Phase 0 complete 2026-07-10**

---

## Phase 1 — Importer skeleton (§1.2, §1.3)

- [x] New console project `src/MobmekApi.LegacyImport` added to `mobmek_api.slnx`; references `MobmekApi`; packages: `Microsoft.Data.SqlClient` 6.1.1, `Npgsql.EntityFrameworkCore.PostgreSQL` (via main project) — builds clean
- [x] Config: `appsettings.json` with `LegacyDb` (MSSQL) + `TargetDb` (Postgres, compose port 5433) connection strings (+ optional `appsettings.local.json` / env-var overrides); CLI flags `--dry-run`, `--phase <name>`
- [x] `LegacyImportMap` tracking table created via raw DDL on startup if missing (`entity_type`, `legacy_id`, `new_id`, `imported_at_utc`, PK on type+legacy_id) — not in `AppDbContext` (§1.3) — `ImportMapStore.cs`; writes join the ambient EF transaction so dry-run rolls them back
- [x] `ImportContext` helper: map lookups (`Map.Get("Customer", 42)`), flag collector, per-phase counters — `ImportContext.cs` (`ImportFlag`, `PhaseStats`)
- [x] Legacy readers: `Legacy*` record per old table + raw-SQL reader class — `Legacy/LegacyRecords.cs` + `Legacy/LegacyDbReader.cs`, 19 tables incl. Mechanics (names fold into job notes); columns verified against restored DB; unused `Afterpay*` columns and empty `Appointments.Car` column deliberately not read
- [x] Timezone helper: Auckland → UTC conversion for `DateTime` and Auckland-date `DateOnly` (§1.4) + unit tests (incl. DST boundary) — `NzTime.cs`, 7 tests green (spring-forward gap shifts +1h, fall-back ambiguity → NZST)
- [x] Report writer: markdown report per §6 (counts, flags; reconciliation totals appended by Phase 4+), written on every run to `legacy-import-report-<ts>.md` (gitignored — contains customer data)
- [x] Pipeline runner: phases in §2 order, one transaction per phase, skip already-mapped rows, `--dry-run` rolls back everything — `Pipeline/ImportPhase.cs` + `Pipeline/ImportPipeline.cs`; rollback verified (map table absent after dry-run)
- [x] **Expected output:** `dotnet run -- --dry-run` connects to both DBs, runs an empty pipeline, prints a report with source counts — verified 2026-07-10: 19 tables / 5,239 rows counted, report written, rollback clean — **Phase 1 complete**

---

## Phase 2 — Customers, cars & lookups (§3.1, §3.2)

- [x] Name-split mapper: first word → `FirstName`, rest → `LastName`, single-word → `LastName="-"` + flag; phone placeholder `"N/A"` + flag; provenance note in `Notes` — `Mappers/CustomerMapper.cs`, 8 tests green (multi-word, single, whitespace collapse, empty/blank phone, blank email/address → null, UTC audit dates)
- [x] Customer import phase (+ duplicate-customer detection flags: same normalized name or phone) — `Phases/CustomerImportPhase.cs`; real run 2026-07-10: 447 imported, 30 suspected-duplicate flags, re-run skips all (idempotent)
- [x] Make/model resolver: first make by lowest `MakeId` (0 makes → `"Unknown"`, >1 → flag), find-or-create `CarMake`/`CarModel` case-insensitively — `Mappers/MakeModelResolver.cs`, 6 tests green (legacy "HILUX" reuses seeded "Hilux"; same model name under different makes stays separate)
- [x] Car import phase: year null → 0 + flag, duplicate rego flag, `CreatedAtUtc`/`UpdatedAtUtc` from old audit dates — `Phases/CarImportPhase.cs`; real run 2026-07-10: 465 imported, 6 duplicate-rego flags, 0 unknown-make/model/year fallbacks fired, seeded makes/models reused case-insensitively (33 makes / 216 models after import)
- [x] Integration check: imported customers/cars served by the live API (login → `GET api/customers` returns imported rows; `POST api/cars` added a car to imported customer #17 → 201, then cleaned up). UI eyeball at http://localhost:3000 recommended but the full stack (auth → controller → service → imported data) is verified
- [x] **Expected output:** full customer+car import ran clean 2026-07-10: 447 customers + 465 cars imported (Postgres: 453/472 incl. 6/7 pre-existing dev rows), 912 map rows, 0 orphans; flags = 30 suspected duplicates + 20 placeholder phones + 170 single-word names + 6 dup regos (all expected from Phase 0); re-run fully skipped (idempotent) — **Phase 2 complete**

---

## Phase 3 — Service catalog & jobs (§3.3, §3.4)

- [x] `Service` → `JobService` catalog import (find-or-create by name, don't duplicate seeded entries) — `Mappers/ServiceCatalogResolver.cs` + `Phases/ServiceCatalogImportPhase.cs`, 3 tests green; reused entries flagged with price comparison
- [x] Job mapper: customer derived via car; status mapping per finalized Phase-0 table (+ unknown → `Completed` + flag); `JobNotes` "Imported details" block assembling `Issue`, old `Notes`, `DateStarted`/`DateFinished`, labour names, mechanic name — `Mappers/JobMapper.cs`, 12 tests green (all 5 real statuses + unknown/null + Active-NewInvoice→Invoiced override; notes block content + omission of absent sections; 4000-char truncation guard)
- [x] JobItem mapper: `"%"`/`"$"` → `MarkupSolution` enum, copy stored computed fields verbatim — `Mappers/JobChildMappers.cs`, tests green (incl. deliberately-inconsistent totals proving no recomputation)
- [x] Labour mapper: hours/rate/total copy; name routed to job notes — `Mappers/JobChildMappers.cs`, tests green
- [x] `JobService` join → `JobServiceLine`: `UnitPrice = Price + AdditionalAmount`, `Quantity = 1` — `Mappers/JobChildMappers.cs`, tests green
- [x] Job import phase (job + children in one pass); totals copied not recomputed — `Phases/JobImportPhase.cs`; real run 2026-07-10: 419 jobs + 526 items + 177 labour + 188 service lines imported, idempotent re-run skips all
- [x] Integration check: imported job served correctly by the live API (detail + items + labour + services endpoints; service line kept legacy snapshot price 250 vs catalog 200); **edit-recompute verified**: labour 2h→3h on an imported job recomputed total 385→485, revert restored 385. UI eyeball at localhost:3000 recommended
- [x] **Expected output:** all 419 old jobs imported 2026-07-10 (Postgres 427 incl. 8 dev rows); `SUM(TotalJobPrice)` of imported jobs = **180,854.19 — matches legacy exactly**; 410/419 totals consistent with children (9 preserve legacy's own drift); statuses: 380 Invoiced / 21 InProgress / 16 Completed / 9 Open / 1 AwaitingParts; flags: 20 job-status-mapped + 2 service-reused (Full Service price 250→200 catalog mismatch for owner review) — **Phase 3 complete**

---

## Phase 4 — Invoices & quotations (§3.5)

- [x] Money mapper shared by all four sources: null → 0 coalescing, `GstRate` = constant 0.15 (Phase 0 finding: legacy tax was 15% *exclusive* on subtotal+labour+shipping−discount — never derive from `TaxAmount/SubTotal`), item mapping with `ItemTotal` fallback `Quantity × ItemPrice` — `Mappers/DocumentMapper.cs`, 9 tests green (incl. deliberately-inconsistent tax proving no recomputation; `AmountPaid` stays null when null; item names >255 chars truncated — 1 real case, full text preserved in report flag)
- [x] Synthetic-job builder for legacy `Invoice`/`Quotation`: title from `IssueName`, status `Invoiced`/`Completed`, provenance note, dates from the document — `Mappers/SyntheticJobBuilder.cs`, 5 tests green (totals stay 0; blank-title fallback; 200-char truncation)
- [x] Legacy `Invoice` + `InvoiceItem` import phase (synthetic job + `DocumentType="Invoice"`) — `Phases/LegacyInvoiceImportPhase.cs`; real run 2026-07-10: 105 imported
- [x] Legacy `Quotation` + `QuotationItem` import phase (synthetic job + `DocumentType="Quotation"`) — `Phases/LegacyQuotationImportPhase.cs`; 123 imported
- [x] `NewInvoice` + items import phase: attach to mapped job, payment fields (`DatePaid`, `ModeOfPayment`, `CashAmount`, `CardAmount`), `Status` carry-over; job status bump to `Invoiced` (Active only; no-op in practice — jobs phase already applied it) — `Phases/NewInvoiceImportPhase.cs`; 448 imported (385 Active / 63 Rejected carried over)
- [x] `NewQuotation` + items import phase: `ValidUntil` → `DueDate`, `IsAccepted` → notes suffix — `Phases/NewQuotationImportPhase.cs`; 49 imported (2 accepted markers)
- [x] Sequence-number assignment pass: per `DocumentType`, order imported docs by original `DateAdded`, number from current max+1 (§3.5) — `Mappers/SequenceNumberAssigner.cs` + `Phases/DocumentSequencePhase.cs` (raw-SQL updates so imported `UpdatedAtUtc` audit dates aren't re-stamped), 3 tests green; result: Invoice 1–569 / Quotation 1–177, gapless, 0 duplicates, 0 chronology violations (dev rows kept 1–16 / 1–5)
- [x] Integration check: imported invoice served by live API with printed number (INV-0127), totals, GST 0.15, paid state, customer + car resolved; items line served (INV-0128); legacy invoice on synthetic job opens (INV-0019, 3 items, provenance note visible on the job); paged list shows all 569; **mark-as-paid verified** on imported unpaid invoice #1022 (paid → amountPaid 552.00 → reverted to original state). PDF/email generation left for owner eyeball in UI (no PDF-only endpoint; email would really send)
- [x] Reconciliation section added to the report (`ReconciliationPhase`, runs last): legacy vs imported counts, `SUM(TotalAmount)`/`SUM(AmountPaid)`/paid counts per source table + per `DocumentType`, job counts, item-line counts — scoped via `legacy_import_map` so dev rows don't pollute it
- [x] **Expected output:** all four document tables imported 2026-07-10, reconciliation **all 17 rows green**: Invoices 105/44,725.10 · Quotations 123/70,076.11 · NewInvoices 448/215,547.57 · NewQuotations 49/71,547.61; `SUM(AmountPaid)` 15,664.17 + 63,594.76; paid counts 42 + 158; 228 synthetic jobs; 1,527 item lines; dry-run + real run + idempotent re-run all clean; flags: 1 item-name-truncated (QuotationItem #96, full text in report) — **Phase 4 complete**

---

## Phase 5 — Appointments (§3.6)

- [x] Appointment mapper: date+time → `StartUtc`, `TimeEnd` null → +1 h, status mapping (incl. `DateCancelled` → `Cancelled`), contact/vehicle text fields, car→customer hard links via map, old `Job.AppointmentId` back-link → new `Appointment.JobId` — `Mappers/AppointmentMapper.cs`, 14 tests green; **new edge found in real data:** 23 rows with `TimeEnd` ≤ start (AM/PM entry slips, e.g. 11:30→00:30) → same +1 h fallback, flagged `appointment-end-adjusted`; `Type` (`Appointment`/`Walk In`) preserved as `[Type: …]` notes suffix
- [x] Appointment import phase — `Phases/AppointmentImportPhase.cs`; real run 2026-07-10: 259 imported, idempotent re-run skips all; statuses land exactly per Phase-0 table (73 Scheduled / 94 Arrived / 85 Completed / 7 Cancelled), 259 car+customer links, 198 job links, 230 GoogleEventIds carried, 0 `EndUtc ≤ StartUtc` in target
- [x] Integration check: linked imported appointment served by live API with customer/car/job all resolved (names + job title match), GCal id and notes intact; calendar range query returns all 66 2025 appointments. Calendar UI eyeball at localhost:3000 recommended. *(Side finding, pre-existing + unrelated to import: `GET api/appointments?from=2025-01-01` 500s — bare dates bind as Unspecified-kind `DateTime` which Npgsql rejects for `timestamptz`; works with `Z`-suffixed UTC timestamps, which is what the frontend sends.)*
- [x] **Expected output:** all 259 appointments imported and reconciled (`Appointments | 259 | 259 | ✅` in report); flags: 23 end-adjusted — **Phase 5 complete**

---

## Phase 6 — Full run, verification & sign-off (§6)

- [ ] Owner takes a **fresh cutover `.bak`** from the host (old system stops taking new work from here) → replace the file in `legacy-backup/` → re-restore
- [ ] Full `--dry-run` against the fresh restored DB: zero failures, all flags reviewed with owner
- [ ] Reset target DB → full real run → complete report saved to `docs/` (or archived alongside)
- [ ] Reconciliation section all-green: counts, `SUM(TotalAmount)` by type, `SUM(AmountPaid)`, job count = old + synthetic
- [ ] Manual sign-off checklist (§6): legacy customer end-to-end, invoice matches an old printout, edit imported customer, add new car/job/invoice to imported customer
- [ ] Flagged-data cleanup pass in the new UI (placeholder phones, `Year = 0`, name splits) — owner task, report is the worklist
- [ ] `dotnet test` green (all mapper/pipeline tests); `dotnet build` clean
- [ ] Post-sign-off teardown: stop `legacy` compose profile, keep the `.bak` archived, drop `legacy_import_map` (or keep until confident), note completion date here
- [ ] **Expected output:** new system is the single source of truth; old data fully usable and editable

---

## Deferred (not in v1, §4)

- [ ] Mechanics → Employees (+ `JobMechanic` assignments)
- [ ] Reminders import
- [ ] - DO NOT DO THIS YET - WE HAVE DUMMY DETAILS IN CURRENT SYSTEM - ONCE WORKING I WILL REMOVE ALL DATA AND IT SHOULD ONLY HAVE EVERYTHING FROM LEGACY DATA - Remove `legacy-mssql` from compose + delete importer project once migration is final
