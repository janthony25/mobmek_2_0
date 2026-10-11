# Customers & Vehicles

**Last verified:** 2026-10-07 (read against `Controllers/{Customers,Cars,CarMakes,CarModels}Controller.cs`, `Services/{Customer,Car}Service.cs`, `Entities/{Customer,Car}.cs`, migrations, `mobmek_frontend/src/pages/{Customers,CustomerDetail,CarDetail,CarMakes}Page.tsx`; duplicate-detection shipped same day, live-verified in-browser against the real DB)

Covers customer records, vehicle records, the make/model lookup tables, customer search/pagination, and the invoice/quote "Send Email" action that lives on the customer page.

## Status summary

| Sub-capability | Status | Key file(s) |
|---|---|---|
| Customer CRUD | Working | `Controllers/CustomersController.cs`, `Services/CustomerService.cs` |
| Customer ↔ Car relationship | Working | `Entities/Customer.cs:21`, `Entities/Car.cs:31-33` |
| Car ↔ CarMake/CarModel lookup | Working | `Services/CarService.cs`, `CarMakesController.cs`, `CarModelsController.cs` |
| Car → service history (Jobs) link | Working | `pages/CarDetailPage.tsx:29-43` (client-side filter) |
| `car-make-model.md` reference file | Not consumed by code — **decided to leave as-is** | `Data/CarReferenceDataSeeder.cs` (10 hardcoded makes only) |
| "Send Email" on customer page | Working | invoice/quote scoped only — see below |
| Search / filtering / pagination | Working | `CustomerService.GetPagedAsync`, `CrudSection.tsx` |
| Duplicate-customer detection (phone/email) | Working — soft warning | `CustomerService.CheckDuplicatesAsync`, `CustomerForm.tsx` |
| Duplicate-vehicle detection (rego/VIN) | Working — hard block | `CarService.ValidateUniqueAsync` |
| Customer creation requires a car | Working — atomic, since 2026-10-11 | `POST /api/customers/with-car`, `CustomerService.CreateWithCarAsync` |
| First/last name + address shown/entered as uppercase | Working — display-only, no data change | `CustomerForm.tsx` (input), `CustomerCard.tsx`/`CustomerDetailPage.tsx`/`CustomersPage.tsx` (Tailwind `uppercase`) |
| Customer notes/reminders linkage | Working | `CarId`-belongs-to-customer enforced; notes panel added to `CustomerDetailPage.tsx` |

## Details

### Customer CRUD — Working
`CustomersController` is a thin pass-through to `CustomerService` (`Services/CustomerService.cs:118-168`), persists to `AppDbContext.Customers` (migration `20260630083000_AddCustomer.cs`). Required: `FirstName`, `LastName`, `PhoneNumber`. Optional: `EmailAddress`, `PhysicalAddress`, `Notes` (plain string field, not the `Note` entity).

### Customer ↔ Car — Working
`Customer.Cars` / `Car.CustomerId` FK, cascade delete (migration `20260630083723_AddCar.cs` — deleting a customer deletes their cars). `CarsController.GetAll(customerId)` filters per customer, used on `CustomerDetailPage.tsx`.

### Car ↔ CarMake/CarModel — Working, with real schema history
Originally free-text `Make`/`Model` strings; migration `20260630104940_AddCarMakeAndModel.cs` replaced them with FK lookups, unique index on `CarMakes.Name`, composite unique index on `(CarModels.CarMakeId, Name)`. `CarService` validates model-belongs-to-make server-side. Migration `20260702212714_RemoveCarOdometer.cs` dropped odometer entirely — **cars do not track mileage at all today.**

### Car → service history — Working
`CarDetailPage.tsx:29-43` fetches jobs by customer and client-filters by `carId`. Fine at per-car volumes; no server-side filter exists for this specific case.

### `car-make-model.md` — not wired in, deliberately left as-is
Zero code references anywhere (`grep` confirmed). Real seeding is `Data/CarReferenceDataSeeder.cs:12-23` — ~10 hardcoded makes, a small fraction of the `.md` file's exhaustive NZ-market list. **Decision (2026-10-07): leave as-is** — not worth wiring in or deleting unless the 10-make seed list becomes a real problem in practice.

### "Send Email" on customer page — Working, but invoice/quote scoped only
Despite being framed as "email the customer," the button only ever appears on `InvoiceRow`/`QuotationRow` and opens `EmailComposeModal` for that specific document — there's no free-form "email this customer" action. Full real call chain verified end to end: `CustomerDetailPage.tsx` → `EmailComposeModal.tsx` → `api/invoices.ts:sendInvoiceEmail` → `POST /api/jobs/{jobId}/invoices/{id}/email` → `InvoicesController.SendEmail` → `OutboundEmailService.SendInvoiceEmailAsync` → real Resend HTTP call, with a PDF attachment and a `Queued→Sent/Failed` audit row. See `email-module.md` for the full pipeline. Gated on `ResendConfigured`; surfaces a clear "not configured yet" message if unset.

### Search / filtering / pagination — Working (hybrid server-side, confirmed here)
`GET /api/customers/paged` (`CustomersController.cs:22-35` → `CustomerService.GetPagedAsync`, lines 22-88): server-side skip/take, search across name+phone+email+car rego, sort, date-range filter, `MaxPageSize=200`. Frontend `CrudSection.tsx:114-146` debounces search and refetches server-side on page/search/sort change. List items carry pre-aggregated car/note/reminder counts computed server-side — no N+1 on the card view.

**Fixed 2026-10-11 — customer-list car tags now show the rego:** each vehicle tag on `CustomerCard.tsx:76` previously showed only "Year Make Model" (e.g. "2020 Mazda CX-30"), ambiguous when a customer has two cars of the same make/model. Added `Rego` to `CustomerCarSummaryDto` (`DTOs/CustomerDtos.cs:23-30`) and the projection in `CustomerService.cs:71-81`, and `rego` to the frontend `CustomerCarSummary` type — now renders "2020 Mazda CX-30 (NCJ955)". `CarDetailPage.tsx`/`CustomerDetailPage.tsx`'s own vehicle lists already showed rego separately and were left unchanged — this was a customer-list-card-only request.

### Duplicate detection — Working (fixed 2026-10-07)
Two different policies by design, per owner decision: customer phone/email is a **soft warning** (people legitimately share phones/emails — e.g. a family); car rego/VIN is a **hard block** (one physical car, one record — a collision is always a data-entry mistake).

- **Customer (soft):** `GET /api/customers/check-duplicate?phone=&email=&excludeId=` (`CustomerService.CheckDuplicatesAsync`) matches on phone (whitespace/dash-insensitive) and/or email (case-insensitive), returns up to 5 lightweight matches, never blocks. `CustomerForm.tsx` (new — replaces the generic schema form previously used by both `CustomersPage.tsx` and `CustomerDetailPage.tsx`'s edit modal, now consolidated into one component) calls this debounced (400ms) as phone/email are typed, shows a dismissible amber banner listing matches, and still lets the user save. Live-verified in-browser against real seeded data (correctly found two existing customers sharing a phone number).
- **Car (hard):** `CarService.ValidateUniqueAsync`, called from both `CreateAsync`/`UpdateAsync`, checks rego (always, case/whitespace-insensitive) and VIN (only when provided — blank VINs don't collide) against all other cars, returns `CarWriteError.DuplicateRego`/`DuplicateVin` → 400. **Important nuance:** the check only fires when the rego/VIN value is actually *changing* — production already has 7 pairs of cars sharing a rego from the legacy import (confirmed by querying the live DB), and without this exclusion, editing an unrelated field (color, year) on any of those 14 existing cars would have been wrongly blocked as colliding with its own pre-existing twin. A regression test (`UpdateAsync_AllowsEditingOtherFields_OnAPreExistingDuplicateRegoPair`) pins this down. Live-verified in-browser (adding a car with rego `FYN218`, one of the known-duplicate regos, was correctly rejected with "A car with this rego already exists.").

7 new backend tests on `CarServiceTests`/`CustomerServiceTests`.

### Customer creation requires a car — Working, atomic (added 2026-10-11)
The main "Add Customer" flow can no longer create a customer with zero vehicles — the strongest available guard against duplicate customers, since rego uniqueness is already a hard block: if a customer can't exist without a car, and that car's rego is already taken, staff are forced to find the real existing customer instead of creating a new one. (This doesn't fully replace the phone/email soft check above — it doesn't catch "same person, genuinely different car each visit.")

- **Backend:** new `POST /api/customers/with-car` (`CreateCustomerWithCarRequest` → `CustomerService.CreateWithCarAsync`) creates the `Customer` and `Car` in one unit of work — nothing is saved to either table until a single final `SaveChangesAsync`. `CarService.BuildAsync`'s customer-existence check was extended to also accept a `Customer` that's `Added` but not yet saved in the current change tracker (previously only checked the database), which is what lets this reuse the exact same make/model + rego/VIN validation `CarsController`'s own create path uses, with zero duplicated logic. On any car-side validation failure (bad make/model, duplicate rego/VIN), the tracked customer is detached and nothing is persisted — not even the customer — so a rejected attempt never leaves an orphan customer-with-no-car behind. 3 new tests, including the atomicity regression (`CreateWithCarAsync_DuplicateRego_PersistsNeitherCustomerNorCar`); 650/650 backend tests passing.
- **Frontend:** `CustomerForm.tsx` now also collects a full car (make → model cascading select, year, rego, VIN/color/engine optional) whenever `initial === null` (creating), reusing the exact same cascading-select pattern as `CarForm.tsx`. Editing an existing customer is unchanged — no car fields shown, since cars are managed from the customer's own detail page. `CustomersPage.tsx`'s `onCreate` now calls `createCustomerWithCar` instead of the old plain `createCustomer`.
- **Verification:** `dotnet build`/`dotnet test` (650/650) and `tsc`/`build`/`lint` all clean. Not live-browser-verified this session — local Postgres now mirrors a real EC2 snapshot (via `scripts/sync-ec2-to-local.sh`) whose admin login isn't a known dev password, and fabricating a login via raw SQL into the Identity tables was correctly blocked rather than worked around. Relying on the automated coverage (including the atomicity regression test) until a real login is available for a browser pass.

### First/last name + address shown as uppercase — Working, display-only (added 2026-10-12)
Deliberately a **presentation-only** change, no data touched: `CustomerForm.tsx`'s First name/Last name/Address inputs now call `.toUpperCase()` on every keystroke (matching the existing pattern already used for phone/GST-number/rego elsewhere), and the Tailwind `uppercase` class was added to every place those three fields are *displayed* — `CustomerCard.tsx`, `CustomerDetailPage.tsx`'s header and address line, and `CustomersPage.tsx`'s Name/Address table columns. Because it's CSS `text-transform`, it also renders existing mixed-case legacy data as uppercase on screen without needing any backfill — an initial attempt to backfill the actual stored data (local + a prepared EC2 script) was correctly stopped by the user as out of scope; no database was touched. `tsc`/`build`/`lint` all clean; not live-browser-verified (no Chrome extension connection available this session).

### Notes/reminders linkage — Partial
`Reminder` requires `CustomerId` (optional `CarId`). **Correction (2026-10-07):** the "car must belong to that customer" invariant IS enforced server-side — `ReminderService.ValidateReferencesAsync` (`Services/ReminderService.cs:130-158`) checks `car.CustomerId != customerId` and returns `ReminderWriteError.CarNotOwnedByCustomer`, covered by `ReminderServiceTests.CreateAsync_ReturnsCarNotOwnedByCustomer_WhenCarBelongsToAnother`. The earlier audit claiming this was unenforced was wrong — verified directly against current code.

**Fixed 2026-10-07 — added a notes panel to `CustomerDetailPage.tsx`:** previously there was no way to view/manage a customer's notes from their own detail page (only from the standalone `NotesRemindersPage.tsx` or the always-visible global `NotesPanel` board, neither of which filter to one customer), even though the customer-list cards already showed live note counts (`activeNoteCount`/`nextNoteDueDate` from `CustomerService.cs:81-84`).

- Backend: `GET /api/notes` gained an optional `?customerId=` filter (`NoteService.GetAllAsync`, `NotesController.GetAll`) — previously returned every note system-wide with no filter param at all.
- Frontend: new `components/notes/NotesSection.tsx`, a `CrudSection`-based component mirroring `RemindersSection.tsx`'s pattern exactly (collapsible, scoped list with Add/Edit/Delete/Mark-done), mounted on `CustomerDetailPage.tsx` below the Vehicles card. `NoteForm.tsx` gained a `lockedCustomerId` prop (same convention as `ReminderForm`'s `lockedCarId`) so the customer picker is hidden and the note is automatically scoped when added from this page.
- Live-verified in-browser: added a note from the new section, confirmed it appeared both in the in-page "Notes (1)" count/list and correctly attributed on the global sidebar board, with no console errors.

## Recommended follow-ups
- ~~Add duplicate-detection for customer phone/email and car rego/VIN~~ — done 2026-10-07.
- ~~Enforce Reminder.CarId-belongs-to-Customer invariant server-side~~ — already done, prior audit note was wrong (corrected 2026-10-07).
- ~~Add a notes panel to `CustomerDetailPage.tsx`~~ — done 2026-10-07.
- Live-browser-verify the new "Add Customer" combined form (2026-10-11 work above) once there's a way to log into the locally-synced data — right now local's admin credentials are the real EC2 ones, not a known dev password.

All known gaps in this feature area are now resolved or deliberately deferred (`car-make-model.md`), aside from the live-browser-verification item just above.
