# Cash Flow Module — Design Document (v2, enterprise revision)

**Status:** Active · **Date:** 2026-07-02 (v2 — enterprise revision of the 2026-07-02 draft)
**Scope:** Cash flow management, bank import & reconciliation, budgets & forecasting, NZ tax obligations, dashboard, reports, and an embedded AI financial assistant for Mobmek.

**Why v2:** the v1 build of Phases 1–2 produced a working but *basic* ledger and forecast. This revision raises the module to the standard of the commercial tools a business would otherwise pay for, benchmarked feature-by-feature against:

| Benchmark | What we take from it |
|---|---|
| **Xero / QuickBooks Online** (SMB accounting) | bank statement import with dedup + auto-match, reconciliation workflow, auto-categorization rules, payee/contact management, split transactions, period lock dates, audit history, bulk recategorization, running balances, CSV export everywhere |
| **Float / Agicap / Fathom** (cash forecasting) | editable scenario parameters, variable-expense run-rate projection (not just committed items), forecast-vs-actual accuracy tracking, budgets with variance, cash calendar, what-if builder |
| **Tesorio / receivables tools** | payment-behaviour modelling, aging with payer risk, slow-payer surfacing |

What we deliberately do **not** copy: double-entry journals, statutory reporting, multi-currency, live bank feeds (Akahu is Phase 7 — CSV import covers the need without a third-party dependency).

---

## 1. Design principles & positioning

1. **This is cash management, not general-ledger accounting.** Mobmek tracks money movements (single-entry cash ledger with paired transfers and split groups), not journals/debits/credits. Owners who need statutory accounts still use Xero/an accountant; Mobmek answers *"how much cash do I have, where is it going, and what's coming?"*
2. **Deterministic numbers, narrated by AI.** Every figure shown to the user is computed by C# services with unit tests. The AI assistant *reads* those figures through tools and explains/advises — it never computes a number that gets displayed as fact.
3. **Estimates are labelled estimates.** All tax figures carry an "estimate — confirm with your accountant/IRD" disclaimer. Facts (actual transactions), imports awaiting review, and forecasts are visually and structurally distinct.
4. **Tax rules are data, not code.** Rates, thresholds, and deadline rules live in configuration (seeded, editable) so legislation changes don't require a code release.
5. **Follow the existing slice pattern.** Entity (`BaseEntity`) → `AppDbContext` config → DTO records → `I{X}Service`/`{X}Service` (scoped, returns DTOs, `AsNoTracking()` reads, `CancellationToken`) → thin controller → service-level xUnit tests → EF migration.
6. **Reuse what exists.** Paid invoices are the workshop's main income source; unpaid invoices with `DueDate` are the receivables book. GST rate comes from the existing `GstSetting` singleton.
7. **Controls and auditability are first-class** *(new in v2)*. Every mutation of financial data is audit-logged with field-level before/after values; reconciled rows are immutable; locked periods reject changes; imports are staged and reviewed before they touch the ledger; destructive bulk actions report exactly what they skipped and why. An accountant reviewing this system should find the same discipline they'd expect from commercial software.
8. **Data entry must be fast or it won't happen** *(new in v2)*. Rules auto-categorize, payees carry defaults, statement import replaces typing, bulk actions fix mistakes across many rows at once. The enemy of a useful cash module is friction.

---

## 2. Domain model

### 2.1 Accounts

**`CashAccount`** — a place money lives. *(unchanged from v1)*

| Field | Type | Notes |
|---|---|---|
| `Name` | string | e.g. "ANZ Business", "Till Cash", "Stripe" |
| `Type` | string | `Bank` \| `Cash` \| `DigitalWallet` \| `CreditCard` |
| `AccountNumber?` | string | display only |
| `OpeningBalance` | decimal | balance at `OpeningDate` |
| `OpeningDate` | DateOnly | ledger starts here |
| `IsArchived` | bool | hidden from pickers, kept for history |

**Current balance is always derived**: `OpeningBalance + Σ(inflows) − Σ(outflows)` since `OpeningDate`. Never stored, so it can't drift.

### 2.2 Transactions (the ledger)

**`CashTransaction`** — one actual cash movement. v2 additions marked ★.

| Field | Type | Notes |
|---|---|---|
| `AccountId` | Guid FK | |
| `Direction` | string | `In` \| `Out` |
| `Amount` | decimal | always positive; direction carries sign; GST-inclusive |
| `Date` | DateOnly | cash date (payments-basis friendly) |
| `Description` | string | |
| `CategoryId` | Guid FK | |
| `PayeeId?` ★ | Guid FK | normalized counterparty (§2.4); `Counterparty` string kept as denormalized display name |
| `Counterparty?` | string | free text; auto-filled from payee when linked |
| `Status` ★ | string | `Pending` \| `Cleared` \| `Reconciled` — see lifecycle below |
| `InvoiceId?` | Guid FK | set when auto-posted from an invoice payment |
| `RecurringTransactionId?` | Guid FK | set when materialised from a schedule |
| `TransferGroupId?` | Guid | pairs the two legs of a transfer; legs excluded from in/out totals |
| `SplitGroupId?` ★ | Guid | groups sibling rows entered as one split payment (§ splits below) |
| `StatementImportId?` ★ | Guid FK | provenance: which import batch created this row |
| `ImportHash?` ★ | string | dedup fingerprint (account + date + amount + normalized description); unique per account when set |
| `GstTreatment` | string | `Taxable` \| `Exempt` \| `ZeroRated` |
| `Notes?` | string | |
| Attachments | 1‑many | §2.9 |

**Status lifecycle:** manual entries default to `Cleared` (the money moved). `Pending` marks entries the user isn't sure have hit the bank yet (e.g. a cheque, a future-dated payment) — pending rows count toward balances but are visually flagged. `Reconciled` is set **only** by completing a reconciliation session (§2.11); reconciled rows are **immutable** (no edit/delete; correction = reverse-and-re-enter, which the audit log records).

**Splits:** one real-world payment covering multiple categories (a trade-store run: tools + consumables) is entered once and stored as sibling `CashTransaction` rows sharing a `SplitGroupId` — same account/date/payee, each row its own amount/category/GST. This mirrors the proven `TransferGroupId` pattern: every existing totals/GST/forecast/report query works unchanged because each split row *is* a normal ledger row. Split groups are edited/deleted as a unit.

**Invoice integration (unchanged key rule):** marking an invoice paid posts ledger rows (routed per `CashFlowSettings`); rejecting reverses them; invoice-linked rows are read-only in the ledger and link back to the invoice/job in the UI.

**Guards, in precedence order:** not-found → invoice-linked → transfer-leg → reconciled ★ → period-locked ★ (§2.12) → validation.

### 2.3 Categories

**`TransactionCategory`** — flat list with a `Group` rollup. *(unchanged from v1 — seeded NZ set, `IsSystem`, `DefaultGstTreatment`, `ExcludeFromOperatingExpense`, archive-not-delete.)*

### 2.4 Payees ★

**`Payee`** — a normalized counterparty ("Z Energy", "Repco", "IRD"), the equivalent of Xero contacts:

| Field | Notes |
|---|---|
| `Name` | unique (case-insensitive) |
| `DefaultCategoryId?` | picking this payee pre-fills the category |
| `DefaultGstTreatment?` | pre-fills GST treatment |
| `Notes?` | |
| `IsArchived` | hidden from pickers, history intact |

Transactions keep the `Counterparty` display string (set from the payee on link) so history survives payee renames/merges and free-text entry still works. Payee pages show per-payee spend history and 12-month totals. Delete is blocked while transactions reference the payee (archive instead).

### 2.5 Auto-categorization rules ★

**`CategorizationRule`** — the engine behind "stop categorizing the same fuel purchase every week":

| Field | Notes |
|---|---|
| `Name` | e.g. "Z Energy → Vehicle & Fuel" |
| `Priority` | int; lowest wins when several match |
| `MatchField` | `Description` \| `Counterparty` \| `Either` |
| `MatchType` | `Contains` \| `StartsWith` \| `Equals` (case-insensitive) |
| `MatchValue` | the text to match |
| `Direction?` | limit to `In`/`Out` |
| `AmountMin?` / `AmountMax?` | optional band |
| `SetCategoryId` | required outcome |
| `SetGstTreatment?` / `SetPayeeId?` | optional outcomes |
| `IsActive` | |

Applied at three points: **statement import** (rows auto-categorized before review, §2.10), **manual entry** (suggest endpoint the form calls as you type), and **retroactively** (`apply-to-existing` endpoint with a preview count, scoped to a filter). Rules never override a category a user set by hand — they fill blanks and drive suggestions.

### 2.6 Recurring transactions

*(unchanged from v1 — template + `Frequency`/`Interval`/`AnchorDate`/`EndDate`/`AutoPost`/`IsPaused`, occurrences computed on the fly, hourly auto-post job.)*

### 2.7 Planned one-off items

*(unchanged from v1 — `PlannedTransaction` with `Status` and `ScenarioTag`; unpaid invoices remain the expected-income book.)*

### 2.8 Budgets ★

**`CategoryBudget`** — a monthly expectation per category (the Float/Fathom "budget vs actual" backbone): `CategoryId` (unique), `MonthlyAmount`, `EffectiveFrom` (DateOnly, month grain), `Notes?`. Deliberately simple — one current monthly figure per category, not a 12-column budget grid (that's a Phase 7 candidate if ever needed).

Used by: the **Budget vs Actual report** (per category per month: budget, actual, variance $, variance %), dashboard over-budget flags, and the forecast's variable-spend source (§3.1 — the run-rate is clamped toward budget when one exists, so a deliberate budget acts as the owner's stated intent).

### 2.9 Attachments

*(unchanged from v1 — `TransactionAttachment` via `IFileStorage`; local now, S3-compatible later.)*

### 2.10 Bank statement import ★

The single biggest enterprise gap in v1: nobody hand-types three months of bank activity. CSV import (every NZ bank exports CSV) with a staged review pipeline:

- **`ImportProfile`** — per-account column mapping, saved once per bank: `AccountId`, `Name`, `DateColumn`, `DateFormat` (e.g. `dd/MM/yyyy`), `DescriptionColumn`, amount shape = `SingleSignedColumn(AmountColumn)` \| `SeparateColumns(DebitColumn, CreditColumn)`, `HasHeaderRow`, `ReferenceColumn?` (appended to description).
- **`StatementImport`** — one upload batch: `AccountId`, `ProfileId`, `FileName`, `Status` (`Reviewing` → `Committed` \| `Discarded`), counts (parsed/duplicate/committed), `CreatedAtUtc`.
- **`StagedTransaction`** — one parsed row awaiting review: parsed date/description/amount/direction, `ImportHash`, `IsDuplicate` (hash already in ledger or batch), `MatchedTransactionId?` (auto-match hit), suggested `CategoryId?`/`PayeeId?`/`GstTreatment?` from rules, `Resolution` (`Import` \| `Skip` \| `MatchExisting`), user-editable category/payee before commit.

**Pipeline:** upload CSV → parse via profile (row-level errors reported, not silently dropped) → **dedup** by `ImportHash` against ledger + batch → **auto-match** unhashed rows against existing uncleared ledger rows (same account, same amount, date ±3 days — catches invoice auto-postings and manual entries; match sets `Resolution=MatchExisting`, which marks the existing row `Cleared` instead of creating a duplicate) → **rules** fill category/payee/GST suggestions → review screen → **commit** creates `CashTransaction`s (`Status=Cleared`, provenance fields set) in one transaction. Uncommitted batches can be discarded without a trace in the ledger.

### 2.11 Reconciliation ★

**`ReconciliationSession`** — the "does the ledger match the bank" ritual, Xero-style: `AccountId`, `StatementDate`, `StatementBalance`, `Status` (`InProgress` → `Completed` \| `Abandoned`), `CompletedAtUtc?`.

Flow: start a session with the statement end date + closing balance → the screen lists all non-reconciled rows dated ≤ statement date → user ticks rows that appear on the statement → live difference = statement balance − (opening reconciled balance + ticked rows) → **Complete** requires difference = 0, stamps ticked rows `Reconciled` (immutable from then on), and records the session as the account's new reconciled checkpoint. The account card shows "reconciled to 31 May — $12,340.50" and warns when the derived balance has unreconciled drift older than 30 days.

### 2.12 Audit trail & period locking ★

- **`CashFlowAuditLog`** — every mutation of ledger-affecting entities (`CashTransaction`, transfers, splits, `Payee`, `CategorizationRule`, `CategoryBudget`, settings, reconciliation completion, import commits): `EntityType`, `EntityId`, `Action` (`Created`/`Updated`/`Deleted`), `Summary` (human line: "Amount 120.00 → 150.00"), `Changes` (JSON array of `{field, old, new}`), `TimestampUtc`. Written by the services (explicit, testable), queryable per entity and globally (paged). Surfaces in the transaction details modal ("History") and an Audit page. Single-user system today, so no actor column; add one when auth lands.
- **Period lock** — `CashFlowSettings.LockDate?` (DateOnly). Any create/update/delete touching a transaction dated on/before the lock date is rejected with `PeriodLocked`. Raising the lock date is itself audit-logged. This is what lets an owner hand figures to their accountant and trust the history won't shift underneath them.

### 2.13 Tax configuration & obligations

*(unchanged from v1 — `TaxProfile` singleton, `TaxObligation` generation 15 months ahead with NZ deadline rules as data, GST/provisional/PAYE/KiwiSaver/ACC estimators, Tax Reserve, reminder integration at T‑14/T‑3. See v1 §2.7 content preserved below.)*

**`TaxProfile`** (singleton): `EntityType`, `BalanceDate`, `GstRegistered`/`GstBasis`/`GstFrequency`, `ProvisionalMethod` + `PriorYearResidualIncomeTax?`, `EstimatedAnnualProfit?`, `PayeFrequency`, `KiwiSaverEmployerRate` (seed 0.035), `AccAnnualEstimate?`/`AccInvoiceMonth?`, `IncomeTaxRateTable` JSON, `SafetyBufferAmount`.

**`TaxObligation`**: `Type` (`GST`, `ProvisionalTax`, `TerminalTax`, `PAYE`, `KiwiSaver`, `ACC`), period, `DueDate`, `EstimatedAmount`, `Status` (`Upcoming` → `Estimated` → `Paid`, paid links the remittance transaction), `CalculationDetails` evidence JSON.

Deadline rules (data, not code): GST due 28th with 15 Jan / 7 May exceptions; provisional 28 Aug / 15 Jan / 7 May (standard, RIT > $5k); terminal 7 Feb / 7 Apr (agent); PAYE+KiwiSaver 20th; ACC at invoice month. GST estimate = 3/23 × taxable inflows − 3/23 × taxable outflows (payments basis from ledger `GstTreatment`; invoice-basis variant from invoice dates). **Tax Reserve** = GST collected-not-remitted + accrued provisional + open-month PAYE/KiwiSaver + ACC accrual.

### 2.14 AI entities

*(unchanged from v1 — `AiInsight` with evidence JSON and dedupe, `AiConversation`/`AiMessage` with tool-call traces.)*

---

## 3. Forecast engine

### 3.1 Core algorithm

`ForecastService.Project(horizonDays, scenario)` builds a daily balance series from **five** sources (v1 had four — the missing one made forecasts systematically optimistic):

1. **Opening position** = Σ current derived balances of non-archived accounts.
2. **Receivables:** unpaid active invoices → expected on `DueDate + payerDelay` (§3.2).
3. **Recurring:** schedule expansion, posted occurrences excluded.
4. **Planned one-offs:** scenario-matched `PlannedTransaction` rows.
5. **Tax obligations:** estimated amounts on due dates (Phase 4 composition point).
6. **Variable-expense run-rate ★:** operating categories don't stop costing money just because no schedule exists. Per operating outflow category (honouring `ExcludeFromOperatingExpense`), project the **trailing-3-month median monthly spend** — *excluding* rows already represented elsewhere (recurring-posted, transfer legs) — spread across the horizon. When a `CategoryBudget` exists it overrides the run-rate (owner's stated intent beats history). This is the Float/Agicap approach and it is the difference between a forecast and a wish.

Shortage detection: first date projected balance < `SafetyBufferAmount`. Horizons 30/90 days (daily) and 12 months (monthly).

### 3.2 Payment-behaviour model

*(unchanged — per-customer median days-late, business-wide fallback, powers forecast + slow-payer surfacing.)*

### 3.3 Scenarios — now editable ★

**`ScenarioSettings`** (singleton, seeded with the v1 table): per scenario (Best/Worst) — receivables collection % and extra delay days, income multiplier, variable-expense multiplier. *Expected* is always the neutral baseline. Editable in settings UI with a "reset to defaults" action; the assumptions drawer on the forecast page shows the live values. Shortage alerts always use *Expected*; *Worst* is the stress test.

### 3.4 Forecast accuracy tracking ★

The feature that separates forecasting products from chart toys: **was the forecast right?**

- **`ForecastSnapshot`** — the nightly job stores one compact row per day: `SnapshotDate`, `Scenario=Expected`, JSON series of `{date, projectedBalance}` for the next 90 days.
- **Accuracy report:** for a chosen past snapshot age (7/30/60 days ago), compare projected balance vs the actual derived balance for the dates since — absolute and % error, charted. The dashboard shows "30-day forecast accuracy: ±6%" so the owner learns how much to trust the projection, and the AI can explain *why* it missed (late payers, unplanned spend).

---

## 4. Dashboard

Single endpoint `GET api/cashflow/dashboard` returns one composed DTO. All v1 cards kept (Current Cash, Expected Income 30d, Upcoming Expenses 30d, Burn Rate, Net Cash Flow, Runway, Forecast Position, Recurring Obligations, Tax Reserve, Upcoming Tax Payments, 12‑month trend, Financial Health Score with per-component evidence) plus ★: **forecast accuracy** (§3.4), **over-budget categories this month** (§2.8), **unreconciled drift warnings** (§2.11), and **every card drills down** — clicking opens the pre-filtered ledger/report behind the number.

Financial Health Score components (weights configurable): runway ≥ 6 months (25) · positive 3‑month net trend (20) · tax reserve funded (20) · overdue receivables < 15% (15) · recurring outflows < 40% of income (10) · reconciliation fresh (10).

---

## 5. Reports

All parameterised (period grain, date range, account filter), JSON + `format=csv`; the report view is print-stylesheet friendly (browser print → PDF, no dependency):

1. **Cash Flow Statement** — opening, in/out by category group, net, closing.
2. **Cash In vs Cash Out** — time series.
3. **Category Breakdown** — totals, % of total, vs prior period, drill-down.
4. **Budget vs Actual ★** — per category per month: budget, actual, variance $/%.
5. **Recurring Expense Report** — monthly-equivalent, annual cost, 12-month trend, next occurrence.
6. **Payee Report ★** — spend per payee, trend, top movers.
7. **Tax Summary** — per obligation type with `CalculationDetails` evidence.
8. **Outstanding Receivables** — aged buckets, per customer, median-days-late.
9. **Upcoming Payables** — recurring + planned + tax, 90 days.
10. **Forecast Report** — scenario series + live assumptions.
11. **Forecast Accuracy ★** — §3.4.
12. **Audit Log ★** — filterable, exportable.

---

## 6. AI Financial Assistant

*(architecture unchanged from v1 — server-side Claude API, SSE streaming, read-only tools over the same services as the UI, deterministic `run_affordability_check`, detector + narrator insight engine with templated fallback.)*

v2 additions to the tool belt: `get_budget_variance`, `get_forecast_accuracy`, `get_payee_trends`, `get_reconciliation_status`. New detectors: `OverBudget` (category > 110% of budget mid-month), `StaleReconciliation` (no completed session in 45 days), `ForecastDrift` (30-day accuracy error > 15%). Full v1 detector table retained (`CashShortage`, `TaxDeadline`, `ReserveShortfall`, `SubscriptionCreep`, `DuplicateRecurring`, `ExpenseVsRevenueGrowth`, `SlowPayer`, `UnusualSpend`).

---

## 7. API surface

```
api/cashaccounts                       CRUD + GET {id}/balance
api/cashtransactions                   CRUD (paged; filter account/category/payee/status/direction/date/search)
                                       list returns running balance when scoped to one account
api/cashtransactions/export            GET — CSV of the current filter
api/cashtransactions/transfer          POST — paired legs
api/cashtransactions/split             POST — split group; PUT/DELETE {splitGroupId} manage the group
api/cashtransactions/bulk              POST — {ids, action: SetCategory|SetStatus|Delete, ...} → per-row outcome report
api/cashtransactions/{id}/attachments  POST/GET/DELETE
api/transactioncategories              CRUD (system rows: rename only)
api/payees                             CRUD + GET {id}/summary (spend history)
api/categorizationrules                CRUD + POST suggest + POST apply-to-existing (preview & commit)
api/categorybudgets                    CRUD
api/importprofiles                     CRUD
api/statementimports                   POST (upload+parse) · GET list/{id} (staged rows) · PUT {id}/rows/{rowId} (resolution)
                                       POST {id}/commit · POST {id}/discard
api/reconciliations                    POST start · GET {id} (candidate rows + live difference)
                                       PUT {id}/toggle/{txId} · POST {id}/complete · POST {id}/abandon
api/cashflowaudit                      GET (paged; filter entityType/entityId/date)
api/recurringtransactions              CRUD + post-occurrence + pause + due
api/plannedtransactions                CRUD
api/scenariosettings                   GET/PUT + POST reset
api/taxprofile                         GET/PUT
api/taxobligations                     GET + recompute + mark-paid
api/cashflow/forecast                  GET ?horizonDays=&scenario=
api/cashflow/forecast/accuracy         GET ?daysAgo=
api/cashflow/dashboard                 GET
api/cashflow/reports/{report}          GET ?from=&to=&grain=&format=json|csv
api/ai/insights                        GET + dismiss
api/ai/conversations                   GET/POST + messages (SSE)
```

## 8. Frontend

No new dependencies beyond the already-approved `recharts`; tables, wizard, and calendar are hand-built on the existing `ui/` primitives per the dependency policy.

- **Cash Flow (ledger)** — the workhorse screen, rebuilt: status badges (pending/cleared/reconciled) and provenance badges (invoice/transfer/split/import), **running balance column** when one account is selected, **bulk select** with an action bar (set category / mark cleared / delete, with skipped-row reporting), date-range presets (this month, last month, this quarter, FY to date, custom), payee filter, **CSV export**, quick-add with payee autocomplete + rule-driven category suggestion, split-entry modal, details modal with attachments + **History (audit)** tab + jump-to-invoice/job link.
- **Import wizard** — upload → choose/create profile (column mapping with live preview of the first rows) → review table (duplicates greyed, matches flagged, editable category/payee per row, skip toggles) → commit summary.
- **Reconcile screen** — statement date + balance inputs, tick-list of candidate rows, sticky live difference, complete (disabled until $0.00) / abandon.
- **Recurring & Planned** — as shipped in v1 (schedules, due-confirm queue, planned items).
- **Forecast** — v1 chart + **cash calendar** (month grid, expected in/out per day, shortage days highlighted) + editable scenario assumptions + accuracy panel.
- **Budgets** — per-category monthly amounts with current-month actual + variance bar inline.
- **Payees / Rules / Audit** — management pages (CrudSection-based; rules form is bespoke for the match/outcome halves).
- **Tax / Dashboard / Reports / AI panel** — per v1 design (Phases 4–6).

## 9. Delivery phases (v2)

| Phase | Ships | Depends on |
|---|---|---|
| **1 — Enterprise ledger & controls** *(remake)* | Payees, categorization rules, split transactions, status lifecycle, period locking, audit trail, bulk operations, running balance, CSV export, date presets, invoice navigation, rebuilt ledger UI, Payees/Rules pages | v1 Phase 1 |
| **2 — Bank import & reconciliation** | Import profiles, CSV staging pipeline (parse/dedup/auto-match/rules), review wizard, reconciliation sessions, account reconciled-status surfacing | 1 |
| **3 — Budgets & forecast, enterprise grade** *(remake of v1 Phase 2 surface)* | CategoryBudget + variance, variable-expense run-rate in forecast, editable ScenarioSettings, ForecastSnapshot + accuracy report, cash calendar, budgets UI | 1 |
| **4 — NZ tax** | TaxProfile, obligation generator, estimators, tax reserve, reminders, Tax tab | 1 (3 for reserve-vs-forecast) |
| **5 — Dashboard & reports** | dashboard endpoint + cards + drill-downs, health score, all 12 reports + CSV | 1–4 |
| **6 — AI assistant** | detectors (v1 set + OverBudget/StaleReconciliation/ForecastDrift), insight engine, Claude tool-use chat, AI panel | 1–5 |
| **7 — Deferred** | Akahu bank feeds, OFX import, 12-column budgets, multi-currency, AIM/ratio provisional, curated tax-updates feed | — |

Each phase lands with service-level xUnit tests and its own migration. v1's shipped code is **evolved in place** — entities gain columns via migration, services gain behaviour with tests; nothing working is thrown away.

## 10. Explicit assumptions & open questions

1. Single business, NZD only, no live bank feeds in v1–6 (CSV import covers ingestion; Akahu is Phase 7).
2. Payments-basis GST default; invoice basis supported via `GstBasis`.
3. Payroll figures are category-derived until a payroll module exists.
4. No double-entry GL — deliberate scope cut.
5. Single-user: audit log has no actor column yet; add when auth lands.
6. Reconciliation assumes statement balances are entered by the user (no feed); the import pipeline's auto-match keeps the tick-list short.
7. **Resolved from v1:** invoice auto-posting routes per `ModeOfPayment` mapping (shipped); charting = recharts (approved).
