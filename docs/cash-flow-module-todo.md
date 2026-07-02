# Cash Flow Module — Implementation Checklist (v2)

Tracks delivery of [`cash-flow-module-design.md`](./cash-flow-module-design.md) **v2 (enterprise revision)**. Section references (§) point into the design doc.

**Status:** v1 Phases 1–2 shipped 2026-07-02 (basic ledger + forecast, kept and evolved in place). v2 restructures delivery into 7 phases; **Phase 1 remake shipped 2026-07-02** (233 service tests passing; UI type-checked + linted, live browser pass still to do) · Phases 2–7 not started.

**What v1 already shipped (foundation, not repeated below):** CashAccount/TransactionCategory/CashTransaction entities with derived balances, NZ seeded categories, transfers, attachments + `IFileStorage`, invoice auto-posting routed per payment mode, RecurringTransaction + PlannedTransaction + on-the-fly occurrence expansion + auto-post job, payment-behaviour model, ForecastService with 3 scenarios + shortage detection, ledger/accounts/categories/recurring/forecast pages, 202 service tests.

---

## Phase 1 — Enterprise ledger & controls (remake) ✅ (shipped 2026-07-02)

### Backend
- [x] `Payee` entity — unique name, `DefaultCategoryId?`, `DefaultGstTreatment?`, archive-not-delete, delete blocked while referenced; `PayeeId?` on `CashTransaction` (`Counterparty` string kept as display name) (§2.4)
- [x] `PayeeService` — CRUD + per-payee summary (12-month spend, transaction count, first/last seen)
- [x] `CategorizationRule` entity + service — priority-ordered matching (field/type/value/direction/amount band → category/GST/payee), `SuggestAsync`, `apply-to-existing` with preview count; one shared matcher for suggest/retro-apply/(Phase 2) import (§2.5)
- [x] Split transactions — `SplitGroupId` sibling rows; `POST split` (shared account/date/payee + ≥2 lines), group update (wholesale replace) / delete as a unit; individual line edits refuse (`SplitLineReadOnly`); `splitGroupId` ledger filter for group loading (§2.2)
- [x] `Status` lifecycle on `CashTransaction` — `Pending`/`Cleared`/`Reconciled`; manual default `Cleared` (DB default backfills v1 rows); reconciled rows immutable (§2.2)
- [x] Period locking — `CashFlowSettings.LockDate?`; create/update/delete (incl. moving a row into the locked span, transfers, splits, bulk) rejected `PeriodLocked`; lock-date changes audit-logged (§2.12)
- [x] `CashFlowAuditLog` entity + `ICashFlowAuditService` — Created/Updated/Deleted with field-level `{field, old, new}` JSON + human summary, written in the same SaveChanges as the mutation (transaction/transfer/split/payee/rule/settings); paged query endpoint (§2.12)
- [x] Bulk operations — `POST cash-transactions/bulk` (`SetCategory`/`SetStatus`/`Delete`) honouring all guards, per-row skipped-with-reason report; status changes allowed on managed rows (bank-side state), content changes not (§7)
- [x] Running balance — per-row balance on single-account views; suppressed when category/payee/direction/status/search filters would thin rows out (date ranges stay eligible)
- [x] CSV export — `GET cash-transactions/export` honouring the active filter, with proper quoting + provenance column
- [x] Ledger filter additions — `PayeeId`, `Status`, `SplitGroupId`; DTO carries `Status`, `PayeeId`, `SplitGroupId`, invoice's `JobId`
- [x] Migration `AddEnterpriseLedger` + DI registrations
- [x] Service tests — 233 passing (31 new): guard precedence, lock boundary dates, reconciled immutability, payee link/defaults/in-use delete, rule match types/priority/constraints, retro-apply preview-vs-commit + protected-row exclusion, split invariants + group ops, bulk skip reporting per action, running balance math incl. paging, CSV shape/escaping, audit payloads

### Frontend
- [x] Ledger rebuild (`CashFlowPage`) — status + provenance badges, running balance column, bulk select + dark action bar (set category / mark cleared / mark pending / delete with skipped-reason toasts), date-range presets (NZ FY-aware), payee + status filters, CSV export, split entry/edit modal, payee picker with default pre-fill + rule suggestion on description blur, details modal with History (audit incl. group entries) + jump-to-job link + reconciled/locked explainers
- [x] **Payees** page — CrudSection with default category/GST, archive badge
- [x] **Rules** page — bespoke When/Then form, apply-to-history with preview confirm dialog
- [x] Period lock setting on the Cash Accounts settings form
- [x] Types + API modules (`payees`, `categorizationRules`, `cashFlowAudit`, split/bulk/export endpoints); routes + sidebar entries
- [x] Gate: `npx tsc -b` + `npm run lint` clean
- [x] UX polish pass (2026-07-02, after user feedback): labeled search/period toolbar, collapsible labeled filter panel with removable filter chips, plain-English button labels; Recurring & Planned got commitment summary cards (due count, monthly income/costs, net, planned 90d), explained due-confirm queue with overdue badges, human cadence text ("Every 2 weeks · Next 15 Jul"), auto-post vs confirm-each-time badges, "Mark posted" quick action on planned items
- [x] Live browser pass — Cash Flow page (toolbar, filter panel, bulk action bar) and Recurring & Planned (cards, tables) screenshot-verified against the running stack, no console errors; split modal + rule-suggestion flow still only type-checked

### Phase-1 leftovers (fast-follows)
- [ ] Ledger row → audit "History" could later become a global Audit page (§5 report 12)
- [ ] Invoice-linked rows: link from the ledger *row* (not just details modal) to the job

---

## Phase 2 — Bank import & reconciliation (§2.10, §2.11)

### Backend
- [ ] `ImportProfile` entity + CRUD — per-account column mapping (date format, single-signed vs debit/credit columns, header row, reference column)
- [ ] `StatementImport` + `StagedTransaction` entities — upload/parse (row-level error capture), `ImportHash` dedup vs ledger + batch, auto-match (same account/amount, ±3 days, non-reconciled) marking matched rows `Cleared`, rule-driven suggestions, per-row resolution (`Import`/`Skip`/`MatchExisting`), transactional commit with provenance (`StatementImportId`, `ImportHash`), discard
- [ ] `ReconciliationSession` entity + service — start/candidates/toggle/live difference/complete (requires $0.00, stamps `Reconciled`)/abandon; account "reconciled to" surfacing + stale-reconciliation warning
- [ ] S3-backed `IFileStorage` implementation + config switch (v1 leftover)
- [ ] Migration, endpoints (§7), service tests (parser shapes, hash stability, match window, commit idempotency, difference math, immutability after complete)

### Frontend
- [ ] Import wizard — upload → profile mapping with live preview → review table (duplicates greyed, matches flagged, editable rows, skip toggles) → commit summary
- [ ] Reconcile screen — statement inputs, tick-list, sticky live difference, complete/abandon
- [ ] Account cards show reconciled-to status + drift warning

---

## Phase 3 — Budgets & forecast, enterprise grade (§2.8, §3)

### Backend
- [ ] `CategoryBudget` entity + CRUD (one monthly amount per category, `EffectiveFrom`)
- [ ] Variable-expense run-rate forecast source — trailing-3-month median per operating category (excluding recurring-posted rows + transfer legs), budget override when set (§3.1 source 6)
- [ ] `ScenarioSettings` singleton — editable Best/Worst parameters seeded from the v1 table + reset endpoint (§3.3)
- [ ] `ForecastSnapshot` nightly persistence + accuracy computation endpoint (`?daysAgo=`) (§3.4)
- [ ] Migration, tests (run-rate exclusions, budget clamp, accuracy error math, snapshot dedup)

### Frontend
- [ ] **Budgets** page — per-category amounts with current-month actual + variance bar
- [ ] Forecast page additions — cash calendar (month grid, in/out per day, shortage highlight), editable assumptions drawer, accuracy panel

---

## Phase 4 — NZ tax (§2.13)

*(scope unchanged from v1 plan)*

### Backend
- [ ] `TaxProfile` singleton — entity type, balance date, GST basis/frequency, provisional method + prior-year RIT, PAYE frequency, KiwiSaver employer rate (seed 0.035), ACC estimate + invoice month, editable `IncomeTaxRateTable` JSON; `SafetyBufferAmount` migrates here
- [ ] `TaxObligation` entity + deadline-rule engine as data — GST 28th w/ 15 Jan & 7 May exceptions; provisional 28 Aug / 15 Jan / 7 May (RIT > $5k); terminal 7 Feb / 7 Apr; PAYE+KiwiSaver 20th; ACC at invoice month
- [ ] Obligation generator — 15 months ahead, nightly + on-demand recompute, upsert without duplicating paid rows
- [ ] Estimators — GST (payments basis from `GstTreatment`, invoice-basis variant), provisional (105% uplift), PAYE/KiwiSaver (payroll-category proportional), Tax Reserve roll-up
- [ ] `mark-paid` links remittance transaction; reminders at T‑14/T‑3 (needs business-level reminder tweak)
- [ ] Wire obligations into `ForecastService`; endpoints; tests per rule/estimator/reserve/idempotency

### Frontend
- [ ] **Tax** tab — obligations timeline, reserve gauge, profile form; estimate disclaimer on every figure

---

## Phase 5 — Dashboard & reports (§4, §5)

### Backend
- [ ] `GET api/cashflow/dashboard` — v1 card set + forecast accuracy, over-budget categories, unreconciled drift; every card carries a drill-down filter payload
- [ ] Financial Health Score — six deterministic components with pass/partial/fail evidence
- [ ] Reports 1–12 (§5) as parameterised queries, JSON + `format=csv`
- [ ] Tests: KPI math, health components, report aggregation + CSV shape

### Frontend
- [ ] **Dashboard** tab — KPI cards with drill-down navigation, 12-month bars + balance line with dashed forecast, health score breakdown
- [ ] **Reports** view — picker, filters, table, CSV download, print-friendly (browser print → PDF)

---

## Phase 6 — AI assistant (§6)

*(v1 plan + v2 additions)*

### Backend
- [ ] `AiInsight`, `AiConversation`/`AiMessage` entities
- [ ] Detectors — v1 set (`CashShortage`, `TaxDeadline`, `ReserveShortfall`, `SubscriptionCreep`, `DuplicateRecurring`, `ExpenseVsRevenueGrowth`, `SlowPayer`, `UnusualSpend`) + v2 (`OverBudget`, `StaleReconciliation`, `ForecastDrift`)
- [ ] `InsightEngine` nightly job — detectors → batched LLM narratives → templated fallback
- [ ] `AiAssistantService` — Claude API server-side, SSE streaming; read-only tools incl. v2 additions (`get_budget_variance`, `get_forecast_accuracy`, `get_payee_trends`, `get_reconciliation_status`); NZ CFO system prompt
- [ ] Endpoints + tests (detector boundaries, affordability math, dedup)

### Frontend
- [ ] AI panel — collapsible drawer: insight feed above streaming chat, evidence expanders

---

## Phase 7 — Deferred (§9)
- [ ] Akahu bank feeds / OFX import
- [ ] 12-column (per-month) budgets
- [ ] Multi-currency; AIM/ratio provisional methods; multi-rate ESCT
- [ ] Curated "NZ tax updates" feed for the AI

---

## Cross-cutting reminders
- Every backend item ships with service-level xUnit tests in the same change; frontend gate is `npx tsc -b` + `npm run lint`.
- Guard precedence on ledger writes: not-found → invoice-linked → transfer-leg → reconciled → period-locked → validation.
- All money figures come from deterministic C# services — the AI only narrates.
- Tax rates/thresholds/deadlines stay in editable data, never hardcoded.
- New frontend dependencies need explicit sign-off first (none anticipated through Phase 6; recharts already approved).
- Every ledger-affecting mutation writes a `CashFlowAuditLog` row — new features must not bypass it.
