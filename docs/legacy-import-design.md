# Legacy Data Import — Design (v1)

Migrates all data from the first-generation system (**MobileMekaniko-Business**, ASP.NET MVC + MSSQL, at `/Users/jun/CLONES/MobileMekaniko-Business`) into the new system (**mobmek_api**, ASP.NET Core + Postgres). After the import, legacy customers, cars, jobs, invoices, quotations and appointments exist as **first-class records** in the new system — fully viewable, editable and extendable, indistinguishable from natively created data.

**Guiding principle (owner decision):** the new system's model is the base. Legacy data conforms to the new schema; we do not weaken new-system invariants (required fields, invoice-belongs-to-job, lookup-table make/model) to accommodate old data.

---

## 1. Approach

### 1.1 Data source

The old database is **hosted** (not local); the owner obtains a **`.bak` backup** from the hosting provider and drops it into **`legacy-backup/`** at the repo root (gitignored — real customer data, never committed). That folder is volume-mounted into a Dockerized SQL Server (`mcr.microsoft.com/mssql/server:2022-latest`, runs on Apple Silicon via Docker Desktop's Rosetta emulation) where the backup is restored. **The `.bak` is needed at Phase 0** — the restore plus the discovery queries that finalize the mapping tables; importer code (Phase 1 + mappers) can be written in parallel before it arrives, but no phase can be *completed* (run + verified) without it. The importer connects to that local copy with `Microsoft.Data.SqlClient` and plain SQL `SELECT`s — no intermediate export files, no scaffolded EF model of the old schema.

We deliberately do **not** point the importer at the live hosted server: a local restore is a frozen snapshot (re-runs read identical data), keeps repeated full-table scans off a production database and off the network, and needs no firewall/credential exposure. If getting a `.bak` from the host proves impossible, the fallback is a one-off connection to the hosted server just to run the same readers — the rest of the pipeline is unchanged either way (readers only need a MSSQL connection string).

MSSQL is migration-time scaffolding only. Once the import is signed off, all data lives in Postgres as first-class rows and the SQL Server container is removed — the new system never reads from MSSQL at runtime.

A `legacy-mssql` service is added to `docker-compose.yml` under a **`legacy` profile** so it only starts when explicitly requested (`docker compose --profile legacy up`). It is temporary tooling, removed once migration is signed off.

### 1.2 Importer shape

A dedicated **console project** `src/MobmekApi.LegacyImport` in the `mobmek_api` solution:

- References the main `MobmekApi` project and writes through the real `AppDbContext` + entities, so all EF mappings, conversions and constraints apply exactly as in production.
- Reads MSSQL with raw SQL into small `Legacy*` record types (one per old table).
- Runs as a CLI: `dotnet run --project src/MobmekApi.LegacyImport -- [--dry-run] [--phase <name>]`.
- **Not** a UI feature or API endpoint — this is a one-time operator migration, not something end users trigger. (Re-runnable while iterating, see §1.3.)

### 1.3 Idempotency & safety

- The importer creates its own tracking table `legacy_import_map (entity_type, legacy_id, new_id uuid, imported_at_utc)` via raw DDL in the Postgres DB. It is **not** part of `AppDbContext` — the app never sees it; it's dropped after sign-off.
- Every migrated row records its mapping. Re-running skips already-mapped rows (insert-only; the old system is retired, so no update-sync is needed). This makes the import safely re-runnable phase by phase while iterating.
- `--dry-run` executes the full pipeline inside a transaction and rolls back, printing the report (§6) without persisting.
- Each phase runs in its own transaction: a phase either fully lands or not at all.

### 1.4 Timezones & audit dates

The old system stored `DateTime.Now` (server local, **Pacific/Auckland**); the new system stores UTC. All old datetimes are converted Auckland → UTC. Original history is preserved:

| Old | New |
|---|---|
| `DateAdded` | `BaseEntity.CreatedAtUtc` |
| `DateEdited` | `BaseEntity.UpdatedAtUtc` |

---

## 2. Import order

Dependencies force this sequence (each phase = one transaction):

1. **Lookups** — CarMakes + CarModels (created on demand from car data), JobService catalog
2. **Customers**
3. **Cars**
4. **Jobs** + JobItems + Labour + JobServiceLines (old jobs, imported as real jobs)
5. **Documents** — legacy `Invoice`/`Quotation` (with synthetic jobs), then `NewInvoice`/`NewQuotation` (attached to jobs from step 4); sequence numbers assigned at the end of this phase
6. **Appointments**

---

## 3. Entity mappings

### 3.1 Customer

Old `Customer` (single `CustomerName`, everything else optional) → new `Customer` (requires `FirstName`, `LastName`, `PhoneNumber`).

| Old | New | Rule |
|---|---|---|
| `CustomerName` | `FirstName` + `LastName` | First word → `FirstName`, remainder → `LastName`. Single-word names: `LastName = "-"`. **Flagged** in report. |
| `CustomerNumber` | `PhoneNumber` | Null/blank → placeholder `"N/A"`. **Flagged** in report for manual cleanup. |
| `CustomerEmail` | `EmailAddress` | direct |
| `CustomerAddress` | `PhysicalAddress` | direct |
| — | `Notes` | `"Imported from legacy system (Customer #<id>)"` |

Duplicates are imported as-is (no dedupe pass); the report lists same-name/same-phone pairs for manual review.

### 3.2 Car

Old `Car` (free-text `CarModel`, many-to-many `Make`, nullable year) → new `Car` (required `CarMakeId`/`CarModelId` lookups, `int Year`).

| Old | New | Rule |
|---|---|---|
| `CarMake` join → `Make.MakeName` | `CarMakeId` | First linked make (lowest `MakeId`); find-or-create in `CarMake` lookup by case-insensitive name. No make → `"Unknown"` make. Multiple makes → first wins, **flagged**. |
| `CarModel` (string) | `CarModelId` | Find-or-create `CarModel` under the resolved make. Null/blank → `"Unknown"` model under that make. |
| `CarYear` | `Year` | Null → `0`, **flagged**. |
| `CarRego` | `Rego` | direct (duplicate regos imported as-is, **flagged**) |
| `CustomerId` | `CustomerId` | via map |
| — | `Vin`, `Color`, `EngineType` | null (didn't exist) |

### 3.3 Service catalog

Old `Service` → new `JobService` (near-identical: Name, Description, Price, IsActive). Find-or-create by name so pre-seeded catalog entries in the new system are reused rather than duplicated.

### 3.4 Job (+ children)

Old `Job` (FK to Car only) → new `Job` (requires `CustomerId` + `CarId`).

| Old | New | Rule |
|---|---|---|
| `Car.CustomerId` | `CustomerId` | derived through the car |
| `CarId` | `CarId` | via map |
| `Title` | `Title` | direct |
| `Issue`, `Notes`, `DateStarted`/`DateFinished`, `LabourName`s, mechanic name | `JobNotes` | Concatenated into a structured "Imported details" block — the new `Job`/`Labour` have no home for these, so they're preserved as text rather than lost. |
| `Status` | `Status` (enum) | **Finalized from real data (2026-07-10)** — `Done` (366) → `Completed`; `In Progress` (12) → `InProgress`; `Scheduled` (2) → `Open`; `Waiting for Parts` (4) → `AwaitingParts`; `Waiting Customer` (35) → `InProgress` + **flagged** (no equivalent in new enum); job has an Active NewInvoice → `Invoiced` overrides. |
| `Odometer` | `Odometer` | null → 0 |
| — | `DiscountType`/`DiscountValue` | `None`/0 — old discounts lived on the invoice, and imported invoices are snapshots (§3.5) so nothing is recomputed |
| `TotalJobPrice`/`TotalJobProfit` | same | copied (null → 0); new backend recomputes only when the job is edited |

Children:

- **JobItem**: direct field map; `MarkupSolution` `"%"` → `Percentage`, `"$"` → `Dollar`; computed fields (`SellingPrice`, `UnitProfit`, `ItemTotal`) copied as stored, not recomputed.
- **Labour**: `LabourHours` → `Hours`, `LabourPrice` → `RatePerHour`, `TotalLabour` → `TotalAmount`. `LabourName` goes into the job's `JobNotes` block (new entity has no name).
- **JobService** (join) → **JobServiceLine**: `UnitPrice = Service.Price + AdditionalAmount` (snapshot), `Quantity = 1`, `LineTotal = UnitPrice`.

### 3.5 Documents — the four legacy tables → one new `Invoice` table

The old system has two generations: legacy `Invoice`/`Quotation` (FK **directly to Car**) and `NewInvoice`/`NewQuotation` (FK to Job). The new system has one `Invoice` entity (`DocumentType` = `"Invoice"`/`"Quotation"`, **required `JobId`**). All money fields are snapshots in the new system — copied verbatim, never recomputed.

**Legacy `Invoice`/`Quotation` — synthetic jobs (owner decision):** each legacy document gets its own auto-created Job on the correct car:

- `Title` = the document's `IssueName`, `Status` = `Invoiced` (invoices) / `Completed` (quotations)
- `JobNotes` = `"Auto-created during legacy import for invoice/quotation #<id>"`
- `CreatedAtUtc` = the document's `DateAdded`; no job children (the document's line items carry the detail)

**Field mapping (all four old tables → new `Invoice`):**

| Old | New | Notes |
|---|---|---|
| `IssueName` | `IssueName` | |
| `LaborPrice` / `LabourPrice` | `LabourPrice` | null → 0 |
| `Discount`, `ShippingFee`, `SubTotal`, `TaxAmount`, `TotalAmount` | same | null → 0 |
| — | `GstRate` | Constant `0.15`. **Confirmed from real data + old code (2026-07-10):** legacy GST was always 15%, but *added on top* of `(SubTotal + labour + shipping − discount)` where legacy `SubTotal` excluded labour (`carInvoice.js`) — so `TaxAmount/SubTotal` is NOT the rate (real ratios scatter 0.146–0.455) and must not be derived. `NewInvoice`/`NewQuotation` are exactly 15%. All money fields are copied verbatim so totals stay correct; only note that legacy documents were tax-exclusive while the new system displays tax-inclusive. |
| `IsPaid`, `AmountPaid` | same | legacy `Invoice` has no `DatePaid` → stays null |
| `DatePaid`, `PaymentTerm`, `ModeOfPayment`, `CashAmount`, `CardAmount` | same | NewInvoice only |
| `DueDate` (invoices), `ValidUntil` (NewQuotation) | `DueDate` | DateTime → DateOnly (Auckland date) |
| `Status` (`Active`/`Rejected`) | `Status` | legacy tables have no status → `"Active"` |
| `Notes` | `Notes` | NewQuotation `IsAccepted == true` appends `"[Accepted in legacy system]"` (no field for it in the new model) |
| items (4 item tables) | `InvoiceItem` | `ItemName`, `Quantity`, `ItemPrice`, `ItemTotal` (null → `Quantity × ItemPrice`). Names longer than the new 255-char column are truncated with a report flag carrying the full original text (1 real case: QuotationItem #96, 1838 chars of pasted job detail). |
| `IsEmailSent` | — | dropped; new system tracks email per `OutboundEmail` row (accepted loss, §7) |

**Sequence numbers:** the new system prints `INV-0001`/`QUO-0001` from a per-`DocumentType` `SequenceNumber` (assigned as max+1). After all documents are inserted, the importer orders each type by original `DateAdded` and assigns numbers starting at the current max+1 in the new DB (1 if empty). Chronological order is preserved within the import; anything created afterwards continues the sequence. **Consequence:** printed numbers will not match old printed numbers — the original legacy id is recorded in `legacy_import_map` and in the report for cross-reference.

### 3.6 Appointment

Old `Appointment` (denormalized text + optional CarId) → new `Appointment` (soft-contact + optional hard links — fits well).

| Old | New | Rule |
|---|---|---|
| `Title` | `Title` | |
| `AppointmentDate + AppointmentTime` | `StartUtc` | Auckland → UTC |
| `TimeEnd` | `EndUtc` | null → start + 1 h. A stored end at/before the start (23 real rows, all AM/PM entry slips like 11:30→00:30) gets the same fallback, **flagged** — the new model requires end after start. |
| `Status` / `DateCancelled` | `Status` | **Finalized from real data (2026-07-10)** — `DateCancelled` set or `Cancelled` (7) → `Cancelled`; `Scheduled` (73) → `Scheduled`; `In-Progress` (93) / `In Progress` (1) → `Arrived`; `Done` (85) → `Completed`; unknown → `Completed`, **flagged** |
| `CustomerName` | `ContactName` | |
| `Contact` | `ContactPhone` | |
| `CarDetails` (+ `CarRego`) | `VehicleDescription` | concatenated |
| `CarId` | `CarId` + `CustomerId` | via map; customer derived from the car |
| old `Job.AppointmentId` back-link | `JobId` | first linked job via map (new side holds the FK) |
| `Notes` (+ `Type` if set) | `Notes` | |
| `GoogleCalendarEventId` | `GoogleEventId` | |
| `QuotedVia` | — | dropped (accepted loss, §7) |

---

## 4. Scope (owner decisions)

**Imported:** Customers, Cars (+ make/model lookups), Service catalog, Jobs + items/labour/service lines, legacy Invoices/Quotations, NewInvoices/NewQuotations, Appointments.

**Not imported:** Mechanics (→ Employees), Reminders, ASP.NET Identity users (new system has its own auth). Mechanic names on jobs are preserved as text in `JobNotes`. Reminders/mechanics can be added as a later importer phase if wanted — the phase structure supports it.

---

## 5. Old → new table reference

| Old (MSSQL) | New (Postgres) |
|---|---|
| `Customer` | `Customer` |
| `Car`, `CarMake` (join), `Make` | `Car`, `CarMake` (lookup), `CarModel` (lookup) |
| `Service`, `JobService` (join) | `JobService` (catalog), `JobServiceLine` |
| `Job`, `JobItem`, `Labour` | `Job`, `JobItem`, `Labour` |
| `Invoice`, `InvoiceItem` | `Invoice` (`DocumentType=Invoice`) + synthetic `Job`, `InvoiceItem` |
| `Quotation`, `QuotationItem` | `Invoice` (`DocumentType=Quotation`) + synthetic `Job`, `InvoiceItem` |
| `NewInvoice`, `NewInvoiceItem` | `Invoice` (`DocumentType=Invoice`), `InvoiceItem` |
| `NewQuotation`, `NewQuotationItem` | `Invoice` (`DocumentType=Quotation`), `InvoiceItem` |
| `Appointment` | `Appointment` |
| `Mechanic`, `Reminder`, Identity tables | — (out of scope v1) |

---

## 6. Import report & reconciliation

Printed at the end of every run (and on `--dry-run`), written to `legacy-import-report-<timestamp>.md`:

- **Counts** per entity: source rows, imported, skipped (already mapped), failed.
- **Flags** (each with legacy id + new id): placeholder phone, single-word name split, `Year = 0`, multiple makes, duplicate rego, unknown job/appointment status raw values (incl. `Waiting Customer` → `InProgress`), suspected duplicate customers.
- **Reconciliation** (must match to sign off): row counts per table pair; `SUM(TotalAmount)` old (all 4 document tables) vs new `Invoice` split by `DocumentType`; `SUM(AmountPaid)` and paid counts; job count = old jobs + synthetic jobs.

**Sign-off checklist** (manual, in the new UI): open a legacy customer → cars, invoices and quotations all visible; open an imported invoice → items and totals match a known old printout; edit an imported customer and add a new car/job/invoice to an imported customer — all work exactly like native data.

---

## 7. Accepted data losses (owner-visible)

- Old printed invoice numbers (legacy ids kept in `legacy_import_map` + report for cross-reference)
- `IsEmailSent` flags; `Appointment.QuotedVia`; mechanic **records** (names preserved as job-note text); reminders (deferred, not lost — old DB is kept)
- Structured `DateStarted`/`DateFinished`/`Issue`/labour names on jobs (preserved as `JobNotes` text)

## 8. Risks / notes

- **Old status strings are assumptions until Phase 0** enumerates real values from the restored DB; mapping tables get finalized then.
- SQL Server on Apple Silicon runs under emulation — fine for a read-only migration source; if the image won't start, fall back to `azure-sql-edge` (also supports `RESTORE DATABASE`).
- The importer bypasses service-layer validation by writing through `AppDbContext` directly — deliberate (services would recompute snapshots); mappers own correctness, covered by tests.
- New-system data created **before** the import keeps its sequence numbers; imported documents number after them. Recommendation: run the real import before heavy production use of invoicing.
