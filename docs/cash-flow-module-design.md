# Cash Flow Module — Design Document

**Status:** Draft for review · **Date:** 2026-07-02
**Scope:** Cash flow management, forecasting, NZ tax obligations, dashboard, reports, and an embedded AI financial assistant for Mobmek.

---

## 1. Design principles & positioning

1. **This is cash management, not general-ledger accounting.** Mobmek tracks money movements (single-entry cash ledger with paired transfers), not journals/debits/credits. Owners who need statutory accounts still use Xero/an accountant; Mobmek answers *"how much cash do I have, where is it going, and what's coming?"*
2. **Deterministic numbers, narrated by AI.** Every figure shown to the user (balances, GST estimates, forecasts) is computed by C# services with unit tests. The AI assistant *reads* those figures through tools and explains/advises — it never computes a number that gets displayed as fact.
3. **Estimates are labelled estimates.** All tax figures carry an "estimate — confirm with your accountant/IRD" disclaimer. Facts (actual transactions) and forecasts (projections) are visually and structurally distinct.
4. **Tax rules are data, not code.** Rates, thresholds, and deadline rules live in configuration (seeded, editable) so legislation changes (e.g. KiwiSaver employer rate steps: 3% → 3.5% on 1 Apr 2026 → 4% on 1 Apr 2028) don't require a code release.
5. **Follow the existing slice pattern.** Entity (`BaseEntity`) → `AppDbContext` config → DTO records → `I{X}Service`/`{X}Service` (scoped, returns DTOs, `AsNoTracking()` reads, `CancellationToken`) → thin controller → service-level xUnit tests → EF migration.
6. **Reuse what exists.** Paid invoices are already the workshop's main income source (`Invoice.IsPaid/AmountPaid/DatePaid/ModeOfPayment`); unpaid invoices with `DueDate` are already the receivables book. The cash-flow module builds on these rather than duplicating them. GST rate comes from the existing `GstSetting` singleton (0.15, added on top of the invoice subtotal — so invoice totals, and therefore ledger amounts, are GST-inclusive).

---

## 2. Domain model

### 2.1 Accounts

**`CashAccount`** — a place money lives.

| Field | Type | Notes |
|---|---|---|
| `Name` | string | e.g. "ANZ Business", "Till Cash", "Stripe" |
| `Type` | string | `Bank` \| `Cash` \| `DigitalWallet` \| `CreditCard` |
| `AccountNumber?` | string | display only |
| `OpeningBalance` | decimal | balance at `OpeningDate` |
| `OpeningDate` | DateOnly | ledger starts here |
| `IsDefault` | bool | target for auto-posted invoice payments |
| `IsArchived` | bool | hidden from pickers, kept for history |

**Current balance is always derived**: `OpeningBalance + Σ(inflows) − Σ(outflows)` since `OpeningDate`. Never stored, so it can't drift. A **`ReconciliationCheckpoint`** (account, date, statement balance) lets the owner pin the derived balance to a bank statement; a mismatch is surfaced as a "unreconciled difference" warning rather than silently adjusted.

### 2.2 Transactions (the ledger)

**`CashTransaction`** — one actual cash movement that has happened.

| Field | Type | Notes |
|---|---|---|
| `AccountId` | Guid FK | |
| `Direction` | string | `In` \| `Out` |
| `Amount` | decimal | always positive; direction carries sign |
| `Date` | DateOnly | cash date (payments-basis friendly) |
| `Description` | string | |
| `CategoryId` | Guid FK | see 2.3 |
| `Counterparty?` | string | supplier / payer name |
| `InvoiceId?` | Guid FK | set when auto-posted from an invoice payment |
| `RecurringTransactionId?` | Guid FK | set when materialised from a schedule |
| `TransferGroupId?` | Guid | pairs the two legs of an account-to-account transfer; transfer legs are excluded from inflow/outflow totals |
| `GstTreatment` | string | `Taxable` \| `Exempt` \| `ZeroRated` — drives GST estimate (amounts are GST-inclusive; GST content = amount × 3⁄23 at 15%) |
| `Notes?` | string | |
| Attachments | 1‑many | see 2.6 |

**Invoice integration (key rule):** when `InvoiceService` marks an invoice paid, it posts a `CashTransaction` (`In`, category "Workshop Sales", `InvoiceId` set, account = default, or split across two transactions when `CashAmount`/`CardAmount` are both present). Un-rejecting/corrections reverse the transaction. This keeps the ledger the single source of truth for cash while invoices remain the source of truth for billing. Manually deleting an invoice-linked transaction is blocked; it must be corrected from the invoice.

### 2.3 Categories

**`TransactionCategory`** — flat list (no deep tree; a `Group` string gives one level of rollup for reports).

- Fields: `Name`, `Direction` (`In`/`Out`/`Either`), `Group` (e.g. "Operating", "Payroll", "Financing", "Taxes"), `IsSystem` (seeded, non-deletable), `DefaultGstTreatment`, `IsArchived`.
- Seeded set (NZ workshop-flavoured): *Inflows:* Workshop Sales, Parts Sales, Other Income, Interest, Capital Introduced, Loan Received, GST Refund, Grant. *Outflows:* Parts & Materials, Subcontractors, Wages & Salaries, PAYE to IRD, KiwiSaver Employer, Rent, Power & Water, Insurance, Vehicle & Fuel, Tools & Equipment, Software Subscriptions, Phone & Internet, Marketing, Bank Fees, Loan Repayment, Owner Drawings, GST Payment, Provisional/Income Tax, ACC Levies.
- System categories `PAYE to IRD`, `GST Payment`, `Provisional/Income Tax`, `KiwiSaver Employer`, `ACC Levies`, `Owner Drawings`, `Loan Repayment`, `Capital Introduced` are flagged `ExcludeFromOperatingExpense` where appropriate so burn rate and P&L-style views aren't distorted by tax remittances and financing movements.

### 2.4 Recurring transactions

**`RecurringTransaction`** — a template + schedule for committed regular income/expenses (rent, insurance, software subs, loan repayments, retainer income).

| Field | Notes |
|---|---|
| `Description`, `Direction`, `Amount`, `CategoryId`, `AccountId`, `Counterparty?`, `GstTreatment` | template for generated items |
| `Frequency` | `Weekly` \| `Fortnightly` \| `Monthly` \| `Quarterly` \| `Annually` |
| `Interval` | every N periods (default 1) |
| `AnchorDate` | first occurrence |
| `EndDate?` | open-ended if null |
| `AutoPost` | if true, occurrence auto-posts a `CashTransaction` on its date; if false it stays "expected — confirm" and the user posts it with one click (default false; safer) |
| `IsPaused` | |

Occurrences are **computed on the fly** for forecasting (no pre-generated rows to keep in sync); a materialised `CashTransaction` back-references the schedule. Amount history is kept implicitly by the posted transactions — the AI's "this subscription increased 24%" insight compares posted amounts against the template over time.

### 2.5 Planned one-off items

**`PlannedTransaction`** — a known future one-off (equipment purchase, expected grant, tax payment override): `Description`, `Direction`, `Amount`, `ExpectedDate`, `CategoryId`, `AccountId?`, `Status` (`Planned` → `Posted`/`Cancelled`), `ScenarioTag?` (`Always` \| `BestCase` \| `WorstCase` — lets "what-if" purchases live only in a scenario).

**Expected income is not duplicated here**: unpaid, non-rejected invoices *are* the expected-income book. The forecast reads them directly (due date + payment-behaviour model, §3.2).

### 2.6 Attachments

**`TransactionAttachment`** — `TransactionId`, `FileName`, `ContentType`, `StorageKey`, `SizeBytes`. Files go to a `FileStorage` abstraction (local `wwwroot/uploads` volume now; S3-compatible later). Receipt/invoice images attach to transactions; the existing invoice print page already covers sales documents.

### 2.7 Tax configuration & obligations

**`TaxProfile`** (singleton, like `GstSetting`/`BusinessDetails`) — everything the estimators need:

| Field | Notes |
|---|---|
| `EntityType` | `SoleTrader` \| `Partnership` \| `Company` — picks 28% flat vs personal marginal scale |
| `BalanceDate` | default 31 March |
| `GstRegistered`, `GstBasis` (`Payments` \| `Invoice`), `GstFrequency` (`Monthly` \| `TwoMonthly` \| `SixMonthly`) | payments basis default (typical ≤ $2M turnover) |
| `ProvisionalMethod` | `StandardUplift` \| `Estimation` \| `None` (ratio/AIM out of scope v1) |
| `PriorYearResidualIncomeTax?` | drives 105% uplift |
| `EstimatedAnnualProfit?` | used when estimating, and for income-tax accrual |
| `PayeFrequency` | `Monthly` (due 20th) \| `TwiceMonthly` (large employer) |
| `KiwiSaverEmployerRate` | seeded 0.035 (rate effective 1 Apr 2026); editable |
| `AccAnnualEstimate?`, `AccInvoiceMonth?` | ACC levies are invoiced by ACC; we accrue ÷12 monthly and place the payment at invoice month |
| `IncomeTaxRateTable` | JSON — seeded with current company rate / personal brackets; editable so legislation changes are data-only |
| `SafetyBufferAmount` | minimum cash the owner wants to keep (default: 1× average monthly outflows); drives shortage alerts |

**`TaxObligation`** — one generated deadline instance: `Type` (`GST`, `ProvisionalTax`, `TerminalTax`, `PAYE`, `KiwiSaver`, `ACC`), `PeriodStart`, `PeriodEnd`, `DueDate`, `EstimatedAmount`, `Status` (`Upcoming` → `Estimated` → `Paid` — paid links to the remittance `CashTransaction`), `CalculationDetails` (JSON evidence: the figures behind the estimate, shown in UI and given to the AI).

A nightly job (and on-demand recompute) regenerates obligations 15 months ahead using NZ deadline rules seeded as data:

- **GST:** due the 28th of the month after period end, with statutory exceptions — period ending 30 Nov → due 15 Jan; period ending 31 Mar → due 7 May.
- **Provisional tax** (standard, March balance date, non-monthly GST): 28 Aug, 15 Jan, 7 May; each instalment = (105% × prior-year RIT) ÷ 3; only if prior RIT > $5,000.
- **Terminal tax:** 7 Feb (or 7 Apr with tax agent — flag on profile).
- **PAYE + KiwiSaver + ESCT:** 20th of following month (monthly filers). Estimated from Payroll category outflows until a payroll module exists; the `Employee` entity can later feed exact figures.
- **ACC:** annual accrual, payment at `AccInvoiceMonth`.

**Tax Reserve** (headline number) = GST collected-but-not-yet-remitted for the open period(s) + provisional/income tax accrued to date − payments made + PAYE/KiwiSaver accrued for the open month + ACC accrual. Displayed as *"you should have at least $X set aside"* next to actual cash.

GST estimate mechanics (payments basis, 15% inclusive): `GST payable = 3/23 × taxable inflows − 3/23 × taxable outflows` over the period, using `GstTreatment` on each transaction; rate read from `GstSetting` so both modules stay consistent.

**Deadline alerts** reuse the existing **Reminder** system: obligation generation upserts reminders at T‑14 and T‑3 days ("GST return & payment due 28 Aug — estimated $4,120"), so tax deadlines appear wherever reminders already surface.

### 2.8 AI entities

- **`AiInsight`** — `Type` (e.g. `CashShortage`, `SubscriptionCreep`, `DuplicateRecurring`, `ExpenseGrowth`, `TaxDeadline`, `ReserveShortfall`, `SlowPayer`, `Affordability`), `Severity` (`Info`/`Warning`/`Critical`), `Title`, `Body` (LLM-written narrative), `Evidence` (JSON of the deterministic figures that triggered it), `Status` (`Active`/`Dismissed`/`Resolved`), `ValidUntil`. Deduped by `(Type, dedupe key)` so the same insight isn't re-raised daily.
- **`AiConversation` / `AiMessage`** — persisted chat threads (role, content, tool-call trace for auditability).

---

## 3. Forecast engine

### 3.1 Core algorithm (deterministic service, fully unit-testable)

`ForecastService.Project(horizonDays, scenario)` builds a **daily balance series**:

1. **Opening position** = Σ current derived balances of all non-archived accounts.
2. For each day in the horizon, sum expected movements from four sources:
   - **Receivables:** unpaid active invoices → expected on `DueDate + payerDelay` (see 3.2), amount = `TotalAmount`.
   - **Recurring:** expand each active `RecurringTransaction`'s schedule (already-posted occurrences excluded).
   - **Planned one-offs:** `PlannedTransaction` rows whose `ScenarioTag` matches.
   - **Tax obligations:** `TaxObligation.EstimatedAmount` on `DueDate` (outflow), plus GST refunds as inflows.
3. Emit `{date, openingBalance, in, out, closingBalance}` per day, rolled up to week/month for charting.
4. **Shortage detection:** first date where projected balance < `SafetyBufferAmount` → drives the dashboard warning and a `CashShortage` insight ("balance falls below buffer in N days").

Horizons: 30/90 days (short-term, daily resolution) and 12 months (long-term, monthly resolution).

### 3.2 Payment-behaviour model (receivables realism)

Per customer, compute **median days-late** = median(`DatePaid − DueDate`) over their paid invoices (fallback: business-wide median; fallback: 0). Expected receipt date = due date + that lag. This one small model powers three features: realistic forecasts, the "slow payers" report, and the AI's "customer payment delays are affecting next month's cash flow" insight.

### 3.3 Scenarios

A scenario = named parameter set applied on top of *Expected*:

| Parameter | Best Case | Expected | Worst Case |
|---|---|---|---|
| Receivables collected | 100%, on due date | per payment-behaviour model | 85%, +14 days extra delay |
| Recurring/estimated income | ×1.10 | ×1.00 | ×0.85 |
| Variable expenses | ×0.95 | ×1.00 | ×1.10 |
| Scenario-tagged planned items | includes `BestCase` | `Always` only | includes `WorstCase` |

Defaults seeded, editable in settings. The forecast chart overlays the three lines; the shortage alert always uses *Expected* (with *Worst Case* shown as the stress test).

---

## 4. Dashboard

Single endpoint `GET api/cashflow/dashboard` returns one composed DTO (one round-trip):

| Card | Definition |
|---|---|
| **Current Cash Available** | Σ derived account balances (per-account breakdown on hover) |
| **Expected Income (30d)** | unpaid invoices expected within 30 days + recurring/planned inflows |
| **Upcoming Expenses (30d)** | recurring + planned outflows + tax obligations due within 30 days |
| **Monthly Burn Rate** | trailing‑3‑month average of *operating* outflows (excludes transfers, tax remittances, financing, drawings) |
| **Net Cash Flow (month)** | inflows − outflows, this month vs last month delta |
| **Cash Runway** | Current Cash ÷ average monthly **net** burn; "cash-flow positive" when net ≥ 0 |
| **Forecast Position (30/90d)** | closing balance from forecast engine, with shortage date if any |
| **Recurring Obligations** | Σ monthly-equivalent of active recurring outflows |
| **Tax Reserve** | §2.7 — reserve needed vs cash actually available above buffer |
| **Upcoming Tax Payments** | next 3 `TaxObligation`s with dates and estimates |
| **Trend graphs** | 12‑month bars: cash in vs cash out; line: end-of-month balance + forecast extension (dashed) |
| **Financial Health Score** | 0–100 composite, see below |

**Financial Health Score** (weights configurable, computed deterministically, explained by AI on click):
runway ≥ 6 months (25) · positive 3‑month net cash flow trend (20) · tax reserve fully funded (20) · receivables overdue < 15% of receivables book (15) · recurring outflows < 40% of average income (10) · reconciliation fresh & no unreconciled difference (10). Each component reports its own pass/partial/fail so the score is auditable, and the AI panel can say *why* it's 68 and what raises it.

---

## 5. Reports

All reports are parameterised queries over the ledger + forecast (period: day/week/month/quarter/year; date range; account filter), returned as JSON for the UI with a CSV export flag:

1. **Cash Flow Statement** — opening balance, cash in by category group, cash out by category group, net, closing; any period granularity.
2. **Cash In vs Cash Out** — time series for the selected grain.
3. **Category Breakdown** — totals + % of total + vs prior period, drill-down to transactions.
4. **Recurring Expense Report** — every active schedule, monthly-equivalent cost, annual cost, last-12-month amount trend (flags increases), next occurrence.
5. **Tax Summary** — per obligation type: accrued, paid, upcoming, with `CalculationDetails` evidence.
6. **Outstanding Receivables** — unpaid invoices aged (current / 1–30 / 31–60 / 60+), per customer, with median-days-late.
7. **Upcoming Payables** — recurring + planned + tax outflows, next 90 days, ordered by date.
8. **Forecast Report** — the three scenario series + assumptions used, exportable.

---

## 6. AI Financial Assistant

### 6.1 Architecture

```
                      ┌────────────────────────────────────┐
                      │  AiAssistantService (backend)      │
  Chat panel  ──SSE──▶│  Claude API (claude-sonnet-5)      │
                      │  system prompt: NZ CFO persona +   │
                      │  business context snapshot          │
                      │  tools (read-only, server-side):   │
                      │   get_dashboard_snapshot           │
                      │   get_transactions(filter)         │
                      │   get_recurring_schedules          │
                      │   get_forecast(scenario, horizon)  │
                      │   get_tax_obligations / tax_profile│
                      │   get_receivables_aging            │
                      │   get_category_trends(period)      │
                      │   run_affordability_check(amount,  │
                      │       monthly_cost, start_date)    │
                      └────────────────────────────────────┘
```

- The Claude API key lives in server configuration; the browser never talks to Anthropic directly.
- **All tools are read-only** and are the *same services* that power the UI, so the AI can never disagree with the screen. `run_affordability_check` is deterministic: it injects a hypothetical planned outflow (+ optional recurring cost) into the forecast and returns the resulting shortage date/buffer impact — the AI narrates the result, it doesn't invent it.
- Responses stream over SSE. Tool-call traces are stored on `AiMessage` for auditability.
- System prompt requirements: NZ tax context (GST 15% inclusive, IRD deadline conventions, provisional tax uplift), **always distinguish "actuals" from "estimates/forecasts"**, cite which tool result each figure came from, end tax answers with the not-professional-advice disclaimer, refuse to fabricate figures not obtainable from tools.

### 6.2 Proactive insights (detector + narrator pattern)

A nightly `InsightEngine` job runs **deterministic detectors** (pure C#, unit-tested); each fired detector emits an evidence JSON; a single batched LLM call turns evidence into plain-English `AiInsight` rows (title + body + recommended action). If the LLM is unavailable, insights still appear with a templated fallback body — detection never depends on the model.

Detectors (v1):

| Detector | Trigger |
|---|---|
| `CashShortage` | forecast (Expected) crosses `SafetyBufferAmount` within 60 days |
| `TaxDeadline` | obligation due ≤ 14 days with estimate ≥ threshold |
| `ReserveShortfall` | cash − buffer < tax reserve required |
| `SubscriptionCreep` | Software Subscriptions trailing-12m up > 15% year-on-year |
| `DuplicateRecurring` | two active schedules, same counterparty ± similar amount |
| `ExpenseVsRevenueGrowth` | 3‑month expense growth rate exceeds income growth rate by > 5 points |
| `SlowPayer` | customer median-days-late > 14 and open balance > threshold |
| `UnusualSpend` | category month total > mean + 2σ of trailing 12 months |
| `IdleSubscription` *(later)* | recurring outflow with no linked activity |

Insights render at the top of the AI panel, newest/critical first, dismissible; dismissing sets `Status=Dismissed` and suppresses the dedupe key for its window.

### 6.3 Conversational scope

Handles the target questions ("Why is cash flow lower this month?" → `get_category_trends` + narrate deltas; "Can I afford to hire?" → `run_affordability_check` with wage + KiwiSaver + ACC loading; "How much GST will I owe?" → `get_tax_obligations` evidence). For **legislation-change awareness**, v1 ships with the seeded rate tables and the model's NZ knowledge, clearly dated; a later iteration can add a curated "NZ tax updates" feed rather than live web search (keeps answers auditable).

---

## 7. API surface (new controllers, existing conventions)

```
api/cashaccounts                     CRUD + GET {id}/balance + POST {id}/reconcile
api/cashtransactions                 CRUD (list: paged, filter by account/category/direction/date/search)
api/cashtransactions/transfer        POST — creates the paired legs
api/cashtransactions/{id}/attachments POST/GET/DELETE
api/transactioncategories            CRUD (system rows: rename only)
api/recurringtransactions            CRUD + POST {id}/post-occurrence + POST {id}/pause
api/plannedtransactions              CRUD
api/taxprofile                       GET/PUT (singleton, like businessdetails)
api/taxobligations                   GET list + POST recompute + POST {id}/mark-paid
api/cashflow/dashboard               GET (composed DTO)
api/cashflow/forecast                GET ?horizonDays=&scenario=
api/cashflow/reports/{report}        GET ?from=&to=&grain=&format=json|csv
api/ai/insights                      GET + POST {id}/dismiss
api/ai/conversations                 GET/POST; POST {id}/messages (SSE stream)
```

## 8. Frontend

- Sidebar section **"Cash Flow"** → `CashFlowPage` with tabs: **Dashboard** (cards + charts, Recharts), **Transactions** (ledger table, quick-add, filters, attachment upload, transfer dialog), **Recurring & Planned** (schedules list with "due — confirm" queue for non-auto-post occurrences), **Forecast** (scenario chart + assumptions drawer), **Tax** (obligations timeline, reserve gauge, profile settings link).
- **AI panel**: right-hand collapsible drawer on the Cash Flow page — insight feed on top, chat below, streaming responses, "evidence" expander per insight showing the deterministic figures.
- Existing patterns reused: `Card`, `PageHeader`, `Badge`, `Modal`, `toast`, `CrudSection` for categories/accounts, `format.ts` for money/dates; types added to `src/types/index.ts`; API clients per resource in `src/api/`.

## 9. Delivery phases

| Phase | Ships | Depends on |
|---|---|---|
| **1 — Ledger** | CashAccount, TransactionCategory (seed), CashTransaction, transfers, attachments, invoice-payment auto-posting, derived balances, transactions UI | nothing |
| **2 — Commitments & forecast** | RecurringTransaction, PlannedTransaction, payment-behaviour model, forecast engine + scenarios, forecast UI | 1 |
| **3 — NZ tax** | TaxProfile, obligation generator + deadline rules, GST/provisional/PAYE/KiwiSaver/ACC estimators, tax reserve, reminder integration, Tax tab | 1 (2 for reserve-vs-forecast) |
| **4 — Dashboard & reports** | dashboard endpoint + cards + charts, health score, all reports + CSV | 1–3 |
| **5 — AI assistant** | insight detectors + engine, Claude tool-use chat, AI panel | 1–4 |

Each phase lands with service-level xUnit tests (in-memory `AppDbContext`, per repo testing rule) and its own migration.

## 10. Explicit assumptions & open questions

1. **Single business, NZD only, no bank feeds in v1** — transactions are entered manually or auto-posted from invoices; bank-feed import (Akahu/CSV) is a natural phase 6.
2. **Payments-basis GST default** — right for most sub-$2M workshops; invoice basis supported via `GstBasis`.
3. **Payroll figures are category-derived** until a payroll module exists; PAYE/KiwiSaver estimates are proportional to Wages outflows.
4. **No double-entry GL** — deliberate scope cut; revisit only if statutory reporting becomes a goal.
5. **Open:** should invoice auto-posting target a user-chosen account per `ModeOfPayment` (cash → Till, card/transfer → Bank) instead of one default account? (Recommended: yes — small mapping table on settings.)
6. **Open:** AIM/ratio provisional methods and multi-rate ESCT are excluded from v1 — confirm acceptable.
