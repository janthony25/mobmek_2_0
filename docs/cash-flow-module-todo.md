# Cash Flow Module — Implementation Checklist

Tracks delivery of [`cash-flow-module-design.md`](./cash-flow-module-design.md). Tick items as they land; each phase is independently shippable. Section references (§) point into the design doc.

**Status:** Phase 1 complete (2026-07-02) · Phases 2–6 not started.

---

## Phase 1 — Ledger ✅ (shipped 2026-07-02)

### Backend
- [x] `CashAccount` entity — derived balance (never stored), archive flag (§2.1)
- [x] `TransactionCategory` entity — direction/group/GST default/`ExcludeFromOperatingExpense` flags (§2.3)
- [x] `CashTransaction` entity — positive amounts + direction, `InvoiceId`/`TransferGroupId` managed-row markers, `GstTreatment` (§2.2)
- [x] `TransactionAttachment` entity + `IFileStorage` abstraction with `LocalFileStorage` (keys are S3-compatible) (§2.6)
- [x] `CashFlowSettings` singleton — per-payment-mode account routing (decision: route by mode, §10.5 → resolved **yes**)
- [x] `CashFlowSeeder` — ~28 NZ system categories with correct GST treatments; `EnsureSystemCategoryAsync` guard for unseeded DBs
- [x] `CashAccountService` — CRUD, grouped-query balances, delete blocked when history exists, clears routing refs
- [x] `TransactionCategoryService` — CRUD, system rows rename/archive-only, in-use delete protection
- [x] `CashTransactionService` — paged/filtered ledger, filter-wide in/out totals excluding transfer legs, paired-leg transfers, attachment upload/download/delete, managed-row edit protection
- [x] `CashFlowSettingsService` — singleton get-or-create, account-id validation
- [x] Invoice auto-posting in `InvoiceService.MarkPaidAsync` — cash→cash account, card→card account, remainder by mode text, fallback to default; idempotent re-post; `RejectAsync` removes postings; skipped entirely when unconfigured (§2.2 key rule)
- [x] Controllers: `cash-accounts`, `transaction-categories`, `cash-transactions` (+`/transfer`, `/attachments`), `cash-flow-settings` (§7)
- [x] DI registrations, dev-startup seeding, migration `AddCashFlowLedger`
- [x] xUnit service tests (~35) — balances, seeding, validation, transfers, attachments, posting/reject/re-post

### Frontend
- [x] Types + API modules (`cashAccounts`, `transactionCategories`, `cashTransactions`, `cashFlowSettings`); `apiPostForm`/`apiUrl` helpers in `client.ts`
- [x] **Cash Flow** page — balance cards, filterable/searchable paged ledger, in/out/net totals, record-transaction modal, transfer modal, details modal with attachments; invoice/transfer rows tagged and protected
- [x] **Cash Accounts** page — account CRUD + Invoice Payment Routing section
- [x] **Categories** page — CRUD with system/archived badges
- [x] Sidebar "Finance" group + routes

### Phase-1 leftovers (fast-follows, not blocking Phase 2)
- [ ] `ReconciliationCheckpoint` — pin derived balance to a bank statement, surface unreconciled difference (§2.1)
- [ ] S3-backed `IFileStorage` implementation + config switch (design principle: swap without touching callers)
- [ ] Invoice-linked rows: navigate from ledger row to the source invoice/job in the UI

---

## Phase 2 — Commitments & forecast (§2.4, §2.5, §3)

### Backend
- [ ] `RecurringTransaction` entity — template fields + `Frequency`/`Interval`/`AnchorDate`/`EndDate`/`AutoPost`/`IsPaused` (§2.4)
- [ ] Add `RecurringTransactionId` FK to `CashTransaction` (deferred out of Phase 1 migration on purpose)
- [ ] Schedule expansion — compute occurrences on the fly (no pre-generated rows); exclude already-posted ones
- [ ] "Due — confirm" flow: `POST {id}/post-occurrence` materialises a `CashTransaction`; auto-post path for `AutoPost=true` (needs the background job runner, below)
- [ ] `PlannedTransaction` entity — one-offs with `Status` and `ScenarioTag` (`Always`/`BestCase`/`WorstCase`) (§2.5)
- [ ] Payment-behaviour model — per-customer median days-late from paid invoices, business-wide fallback (§3.2)
- [ ] `ForecastService.Project(horizonDays, scenario)` — daily series from: current balances + receivables (unpaid invoices at due date + lag) + recurring occurrences + planned items + (Phase 3) tax obligations (§3.1)
- [ ] Shortage detection — first date projected balance < safety buffer (buffer setting lives on `TaxProfile` §2.7; until Phase 3, add it to `CashFlowSettings` or default to 0)
- [ ] Scenario parameter sets (Best/Expected/Worst) — seeded defaults, editable (§3.3)
- [ ] **Background job runner** (hosted service) — first consumer: auto-post due recurring occurrences; Phase 3 and 5 reuse it
- [ ] Endpoints: `recurringtransactions` CRUD + pause + post-occurrence, `plannedtransactions` CRUD, `GET api/cashflow/forecast?horizonDays=&scenario=` (§7)
- [ ] Service tests: schedule expansion edge cases (intervals, end dates, month-length), forecast composition, shortage date, scenario application, payment-lag math

### Frontend
- [ ] **Recurring & Planned** tab/page — schedules list with monthly-equivalent cost, pause, "due — confirm" queue; planned one-offs with scenario tag
- [ ] **Forecast** view — 30/90-day + 12-month chart with three scenario lines, assumptions drawer, shortage warning banner
- [ ] Decide charting approach first (dependency policy: no new packages without sign-off) — in-house SVG vs. adding Recharts (design doc assumed Recharts, §8)

---

## Phase 3 — NZ tax (§2.7)

### Backend
- [ ] `TaxProfile` singleton — entity type, balance date, GST basis/frequency, provisional method + prior-year RIT, PAYE frequency, KiwiSaver employer rate (seed 0.035 as of 1 Apr 2026), ACC estimate + invoice month, editable `IncomeTaxRateTable` JSON, `SafetyBufferAmount`
- [ ] `TaxObligation` entity — type, period, due date, estimated amount, status, `CalculationDetails` evidence JSON
- [ ] Deadline-rule engine (rules as data): GST due 28th with 15 Jan / 7 May exceptions; provisional instalments 28 Aug / 15 Jan / 7 May (standard method, RIT > $5,000); terminal tax 7 Feb / 7 Apr (agent flag); PAYE+KiwiSaver 20th; ACC at invoice month
- [ ] Obligation generator — regenerate 15 months ahead, nightly (job runner) + on-demand `POST recompute`; upsert without duplicating paid rows
- [ ] GST estimator — payments basis from ledger `GstTreatment` (rate/(1+rate) of taxable flows, rate from `GstSetting`); invoice-basis variant from invoice dates
- [ ] Provisional estimator — 105% uplift ÷ instalments; estimation method from `EstimatedAnnualProfit`
- [ ] PAYE/KiwiSaver estimator — proportional to Payroll-category outflows (until a payroll module exists, §10.3)
- [ ] Tax Reserve calculation — GST collected-not-remitted + accrued provisional + open-month PAYE/KiwiSaver + ACC accrual
- [ ] `mark-paid` on an obligation links the remittance `CashTransaction`
- [ ] Reminder integration — upsert reminders at T-14/T-3 for each obligation (reuse existing Reminder system; note: reminders currently require a customer — needs a design tweak for business-level reminders)
- [ ] Wire tax obligations into `ForecastService` as outflows at due dates
- [ ] Endpoints: `taxprofile` GET/PUT, `taxobligations` list/recompute/mark-paid (§7)
- [ ] Service tests: each deadline rule (incl. the two GST exceptions), each estimator against hand-computed figures, reserve math, generator idempotency

### Frontend
- [ ] **Tax** tab — obligations timeline, reserve gauge (reserve needed vs. cash above buffer), profile settings form
- [ ] "Estimate — confirm with your accountant/IRD" disclaimer on every tax figure (design principle #3)

---

## Phase 4 — Dashboard & reports (§4, §5)

### Backend
- [ ] `GET api/cashflow/dashboard` — one composed DTO: current cash, expected income 30d, upcoming expenses 30d, burn rate (trailing-3-month operating outflows, honouring `ExcludeFromOperatingExpense`), net cash flow vs last month, runway, forecast position + shortage date, recurring monthly-equivalent, tax reserve, next 3 tax payments, 12-month trend series
- [ ] Financial Health Score — six deterministic components with per-component pass/partial/fail evidence (weights configurable) (§4)
- [ ] Report queries (JSON + `format=csv`): cash flow statement, in-vs-out series, category breakdown w/ prior-period delta, recurring expense report w/ 12-month trend, tax summary, receivables aging (current/1–30/31–60/60+ w/ median-days-late), upcoming payables 90d, forecast report w/ assumptions (§5)
- [ ] Service tests: KPI math (burn excludes transfers/tax/financing; runway; health components), report aggregation + CSV shape

### Frontend
- [ ] **Dashboard** tab — KPI cards, 12-month in/out bars + balance line with dashed forecast extension, health score with component breakdown
- [ ] **Reports** view — report picker, period/grain/account filters, table + CSV download

---

## Phase 5 — AI assistant (§6)

### Backend
- [ ] `AiInsight` entity — type/severity/title/body/evidence JSON/status/`ValidUntil`, deduped by (type, key)
- [ ] `AiConversation`/`AiMessage` entities — persisted threads incl. tool-call traces
- [ ] Deterministic detectors (pure C#, unit-tested): `CashShortage`, `TaxDeadline`, `ReserveShortfall`, `SubscriptionCreep`, `DuplicateRecurring`, `ExpenseVsRevenueGrowth`, `SlowPayer`, `UnusualSpend` (§6.2 table)
- [ ] `InsightEngine` nightly job — run detectors → one batched LLM call writes narratives → templated fallback bodies when the model is unavailable
- [ ] `AiAssistantService` — Claude API (server-side key via user-secrets/env; never the browser), streaming over SSE
- [ ] Read-only tools backed by the *same services as the UI*: dashboard snapshot, transactions, recurring, forecast, tax obligations/profile, receivables aging, category trends, deterministic `run_affordability_check` (§6.1)
- [ ] System prompt: NZ CFO persona, actuals-vs-estimates rule, cite tool sources, tax disclaimer, refuse to fabricate figures
- [ ] Endpoints: `ai/insights` list/dismiss, `ai/conversations` + SSE messages (§7)
- [ ] Tests: every detector's trigger/no-trigger boundary, affordability check math, insight dedup

### Frontend
- [ ] AI panel on Cash Flow page — collapsible drawer: insight feed (severity-ordered, dismissible, per-insight evidence expander) above a streaming chat (§8)

---

## Phase 6 — Deferred (§10)
- [ ] Bank feed / CSV import (Akahu or bank CSV) with dedup against manual entries
- [ ] Reconciliation UI on top of `ReconciliationCheckpoint`
- [ ] AIM/ratio provisional methods, multi-rate ESCT (§10.6 — confirmed out of v1)
- [ ] Curated "NZ tax updates" feed for the AI (kept out of v1 for auditability, §6.3)

---

## Cross-cutting reminders
- Every backend item ships with service-level xUnit tests in the same change (repo testing rule); frontend gate is `npx tsc -b` + `npm run lint`.
- All money figures shown to users come from deterministic C# services — the AI only narrates (design principle #2).
- Tax rates/thresholds/deadlines stay in editable data, never hardcoded (design principle #4).
- New frontend dependencies (e.g. a chart library) need explicit sign-off first (frontend dependency policy).
