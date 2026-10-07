# Cash Flow & GST Reporting

**Last verified:** 2026-10-07 (read against `Controllers/{CashAccounts,CashTransactions,CashFlowAudit,CashFlowForecast,CashFlowSettings,CategorizationRules,RecurringTransactions,PlannedTransactions,Payees,TransactionCategories,GstReport,GstSettings}Controller.cs`, matching `Services/`/`Entities/`, `docs/cash-flow-module-design.md`/`-todo.md`, 461 service-layer test cases)

This is the largest feature area. `docs/cash-flow-module-design.md`/`-todo.md` (the "v2 enterprise" revision, 2026-07-02) is **confirmed accurate, not stale** — no code post-dates that doc's Phase 1 claim. Memory's "Phase 2 = bank import + reconciliation" is correct as a *plan*, not as anything built.

## Status summary

| Sub-capability | Status | Key file(s) |
|---|---|---|
| Ledger core (accounts, transactions, transfers, splits) | Working | `Services/CashTransactionService.cs` |
| Payees & categorization rules | Working | `Services/PayeeService.cs`, `CategorizationRuleService.cs` |
| Bank reconciliation | **Planned only — dead status value** | `CashTransaction.Status == "Reconciled"` is never set anywhere |
| Bank statement import (CSV/OFX) | **Planned only** | no entity/endpoint/UI exists |
| Recurring transactions | Working — genuinely auto-posted | `Services/RecurringTransactionPostingJob.cs` |
| Planned (one-off) transactions | Working, manual only | `Controllers/PlannedTransactionsController.cs` |
| Forecast | Partial — v1 scope (4 of 6 sources) | `Services/ForecastService.cs` |
| Category budgets | **Planned only** | nothing exists |
| GST Report | Working, confirmed review-only | `Services/GstReportService.cs` |
| GST / Tax Settings | Working, GST-rate only — not full NZ tax obligations | `Controllers/GstSettingsController.cs`, `TaxSettingsPage.tsx` |
| Transaction Categories | Working | `Controllers/TransactionCategoriesController.cs` |
| Audit trail & period locking | Working | `Services/CashFlowAuditService.cs` |

## Details

### Ledger core — Working
Full CRUD on accounts/transactions, derived balances, transfers, splits (with group IDs), bulk ops (set category/status/delete with per-row skip reasons), CSV export, attachments (10MB cap), running balance in filtered views. Guard precedence is exactly as documented: not-found → invoice-linked → transfer-leg → reconciled → period-locked → validation. Period lock (`CashFlowSettings.LockDate`) and audit logging are wired into every mutating call. 26+8 tests.

### Payees & categorization rules — Working
Full CRUD, archive-not-delete, spend-history summary (Payees); suggest + apply-to-existing with a commit flag (Categorization Rules). 6+7 tests.

### Reconciliation — key finding: effectively does not exist
No `ReconciliationSession`/`ImportProfile`/`StatementImport`/`StagedTransaction` entity exists anywhere. `CashTransaction.Status` has a `"Reconciled"` value, but **nothing in the codebase ever sets it** — it's a dead enum value referenced only in guard checks, DTO comments, a status-filter dropdown, and UI copy that implies a feature that was never built. Don't confuse this with `CashFlowAuditController` — that's a genuine, working change-history/audit-log feature (who-changed-what-field-when), a *different* thing the design doc also calls "audit trail." Reconciliation is the distinct, unbuilt one.

### Bank import — does not exist
No CSV/OFX import, no bank-statement entity, no staging table. Confirmed by repo-wide grep. **All cash movements are 100% manual entry today**, plus invoice-payment auto-posting and recurring auto-post (below).

### Recurring & planned transactions — Working (recurring genuinely scheduled)
`RecurringTransactionPostingJob` is a real `BackgroundService` (hourly `PeriodicTimer`, runs on startup then every tick) that posts due occurrences automatically — but **only when `RecurringTransaction.AutoPost == true`** (default `false`); otherwise it sits in a manual "due — confirm" queue. `PlannedTransaction` (one-off expected items) is purely manual/informational, never auto-posted. 10+6+5 tests.

### Forecast — Partial, v1 scope only
`ForecastService` builds from 4 sources: opening balance, receivables (unpaid invoices + payment-lag model), recurring schedule expansion, planned one-offs. Missing vs. design: no tax-obligation source, no variable-expense run-rate source — the frontend literally tells the user "tax obligations aren't included yet." **Scenario multipliers (best/worst case) are hardcoded constants in `ForecastService.cs`, not an editable `ScenarioSettings` entity** — despite the design calling these "now editable," no such entity exists; the UI's "show assumptions" drawer is static text, not a settings form. No forecast-accuracy tracking (`ForecastSnapshot`) exists. 9 tests.

### Category budgets — does not exist
No entity, controller, migration, or UI at all. Design-doc scope, entirely unbuilt.

### GST Report — Working, confirmed review-only
`GstReportController.cs` docstring verbatim: "Review only — does not affect what's filed." Computed purely from `CashTransaction` rows, no write path, no IRD/tax-authority integration anywhere. Frontend shows the same disclaimer verbatim. **Memory's claim that this is review-only and never touches actual filing is confirmed true in code.**

### GST/Tax Settings — Working, but narrow
Just a singleton GST rate (`GstSettingsController`), snapshotted onto invoices at creation. `TaxSettingsPage.tsx` is literally just this GST-rate form — it is **not** the design's broader `TaxProfile`/`TaxObligation` concept (provisional tax, PAYE, KiwiSaver, ACC, deadline engine), which is entirely unbuilt.

### Transaction Categories — Working
CRUD, system categories restricted (rename/archive only, delete blocked), archive-not-delete when in use.

### Audit trail & period locking — Working
`CashFlowAuditLog` entity + service: field-level before/after values, human summary, paged/filterable. Period lock enforced on every mutating path (create/update/delete/bulk/transfer/split).

### Tests
461 service-layer `[Fact]`/`[Theory]` cases across this module. No tests exist for bank import, reconciliation, budgets, scenario settings, or forecast accuracy — consistent with none of those being built.

## Drift vs. design doc

| Design doc item | Reality |
|---|---|
| Bank statement import | Not built |
| Reconciliation sessions | Not built — dead status value |
| Category budgets | Not built |
| Forecast item 6 (variable-expense run-rate) | Not built |
| Editable `ScenarioSettings` | Not built — hardcoded constants |
| `ForecastSnapshot` / accuracy tracking | Not built |
| `TaxProfile`/`TaxObligation` (provisional tax, PAYE, KiwiSaver, ACC) | Not built |
| Dashboard endpoint / Financial Health Score | Not built |
| 12 reports | Not built beyond GST report |
| AI assistant | Not built |
| Phase 1 (ledger, payees, rules, splits, status lifecycle, period lock, audit, bulk ops, CSV export) | **Shipped and verified working** |

**Bottom line:** module is exactly where its own todo checklist says it is. Everything benchmarked against Xero/Float in the v2 design (bank import, reconciliation, budgets, editable scenarios, forecast accuracy, NZ tax obligations, dashboard, reports, AI) remains unbuilt — this is Phase 2+ work, not drift or regression.

## Recommended follow-ups
- If "Reconciled" status is confusing in the UI (filter dropdown, guard messages) while unreachable, either hide it until the feature ships or remove it.
- Bank import/reconciliation is the highest-leverage next phase per the existing design doc — no new design work needed, just build time.
