# Xero Integration — Implementation Checklist (v1)

Tracks delivery of [`xero-integration-design.md`](./xero-integration-design.md). Section references (§) point into the design doc.

**Status:** Phase 0 shipped and live-verified 2026-10-17 — connected to the real "Mobile Mekaniko" Xero org (see `docs/features/xero-integration.md`). Phases ship in order; each leaves the app fully working (sync stays cleanly disabled until Phase 0's connection exists, §2).

---

## Phase 0 — Xero-side setup, OAuth connect flow & config plumbing (§6)

- [x] Register a Xero developer app at `developer.xero.com` (owner) — "Mobmek Integration" app, connected to the real "Mobile Mekaniko" Xero org
- [x] Decide + sign off the Xero client dependency — hand-rolled `HttpClient` calls against `login.xero.com`/`identity.xero.com`/`api.xero.com` (no new NuGet dependency, no sign-off friction)
- [x] `XeroConnection` entity (singleton, encrypted token fields via `IDataProtector`) + migration `AddXeroConnection`
- [x] Admin-only "Connect to Xero" settings page: OAuth2 Authorization Code flow, callback endpoint, stores tokens + tenant id — `XeroSettingsPage.tsx`/`XeroCallbackPage.tsx`, `XeroIntegrationController.cs`, gated on new `Permissions.ManageIntegrations`
- [x] Token-refresh hosted job (daily) — `XeroTokenRefreshJob.cs`
- [ ] Create the Xero webhook subscription (owner, against the same app) + store the signing key as a secret (env/user-secrets) — deferred to Phase 2, not needed yet
- [ ] Settings page: default Xero sales account-code dropdown (populated via `accounting.settings.read`) — deferred to Phase 1, not needed yet
- [x] **Expected output:** clicking "Connect to Xero" completes a real OAuth consent against a real Xero org and `XeroConnection.Status = Connected` — **verified live 2026-10-17** against the real "Mobile Mekaniko" org, all 5 granted scopes confirmed in the DB row. Found and fixed one real bug in the process: Xero's old `accounting.transactions` scope no longer exists under their current granular-scope model (`invalid_scope` on first attempt) — corrected to `accounting.invoices`/`accounting.payments` (§6). Disconnect/reconnect and the daily auto-refresh are not yet separately re-verified.

---

## Phase 1 — One-way invoice push (§4)

**Status:** Live-verified end to end 2026-10-17 — real test invoice (INV-0671) pushed successfully to the "Mobile Mekaniko" Xero org. See `docs/features/xero-integration.md`.

- [x] `XeroSyncItem` entity + `XeroSyncAction` enum + `AppDbContext` config (unique partial index on `InvoiceId` where `Action = 'PushInvoice'`) — migration `AddXeroSyncPhase1`
- [x] `Invoice.XeroInvoiceId`/`XeroSyncStatus`/`XeroLastSyncedUtc`/`XeroLastError` + `Customer.XeroContactId` + `XeroConnection.DefaultSalesAccountCode` — same migration. Hit and fixed a real bug: the enum column's EF-generated default backfilled existing rows with `""` instead of `"NotSynced"`, which would have thrown reading back every pre-existing invoice — fixed with an explicit `.HasDefaultValue()`, migration regenerated.
- [x] `IXeroAccountingClient`/`XeroAccountingClient`: Contacts find-by-email/create, Invoices create-or-update (POST upsert via `InvoiceID`) and void, Accounts list — hand-rolled `HttpClient`, no SDK
- [x] Enqueue hooks: `InvoiceService.GenerateAsync`/`AcceptQuotationAsync` → `PushInvoice`; `RejectAsync` → `VoidInvoice`; no-op when `IXeroAuthService.IsConfigured` is false (same pattern as the calendar sync's `AppointmentService` hooks)
- [x] Contact resolution: match-by-email else create, store `XeroContactId`, reuse on subsequent invoices
- [x] `XeroSyncJob` (hosted, pattern: `CalendarSyncJob`): invoice/line/GST/account-code mapping per §4 table (tax-inclusive, fixed `OUTPUT2` tax type, `LineAmountTypes: "Inclusive"`), idempotent push-or-update, void no-ops when never pushed, backoff ladder, also no-ops until a default sales account code is set
- [x] Settings page: default Xero sales account-code dropdown (`GET/PUT .../accounts`, `.../default-account-code`), gated on `Connected`
- [x] Service tests: 8 `XeroAccountingClientTests` (request/response mapping, null-omission, error surfacing) + 8 `XeroSyncJobTests` (contact reuse/find/create, create-vs-update push, void with/without a prior push, failure backoff + per-invoice error, unconfigured/no-account-code no-ops) + 6 `InvoiceServiceTests` enqueue cases — all against fakes, no real Xero calls. 715/717 full suite passing (the 2 failures are the pre-existing real-Postgres `InvoiceSequenceIndexTests`, unrelated, blocked on the same Docker hang)
- [x] **Expected output:** generating a real invoice in Mobmek produces a real, correctly-totaled `AUTHORISED` invoice in the connected Xero org within ~1 minute — **verified live 2026-10-17**: INV-0671 ($11.00) pushed successfully after a real bug fix (Xero requires a `DueDate`, which Mobmek can leave unset — now defaults to the issue date). Reject-voids the Xero copy is not yet separately live-tested (push path is proven; void path is the same job, unit-tested, lower risk).

---

## Phase 1.5 — Decision checkpoint (§5, §9 step 3)

- [ ] Confirm with the owner: derive `Invoice.IsPaid` from a tagged (`Source: App`/`Xero`) payment list instead of a boolean flag, before writing any Phase 2 code — this changes `MarkPaidAsync`/`InvoicePartialPayment` behavior that's already shipped and tested

---

## Phase 2 — Payment status, two-way (§5)

- [ ] `InvoicePartialPayment.Source` (`App`/`Xero`) + `XeroPaymentId?` — migration
- [ ] Rework `IsPaid` to a computed property (`Sum(payments) >= TotalAmount`); update `MarkPaidAsync`/partial-payment flows to insert tagged `App` rows and enqueue `PushPayment` instead of just flipping a flag
- [ ] `XeroWebhookEvent` entity + `POST /api/integrations/xero/webhook`: signature verification, fast ack (<5s), enqueue-only
- [ ] Background webhook processor: fetch the real Invoice/Payment from Xero, insert/upsert a `Source: Xero` payment row
- [ ] Reconcile job using the Journals API (bookmark last-processed journal number) as the missed-webhook fallback
- [ ] `GET /api/integrations/xero/status` (admin): connected, outbox depth, oldest pending, last webhook processed, last reconcile journal #, last error
- [ ] Tests: payment derivation across mixed-source rows (no double count), webhook signature valid/invalid/replayed, reconcile catch-up after a simulated missed webhook
- [ ] **Expected output:** marking an invoice paid in Mobmek creates a real `Payment` in Xero against the mirrored invoice; applying a bank-reconciled payment in Xero marks the Mobmek invoice paid within the webhook delay (or the next hourly reconcile at worst)

---

## Phase 3 — Strategic revisit (§0, §9 step 5)

- [ ] After Phase 2 has run in production for a reasonable period, explicitly decide whether to shelve the in-house Cash Flow v2 bank-import/reconciliation phase in favor of Xero owning that

---

## Deferred (not in v1, §8)

- [ ] Quotations → Xero Quotes
- [ ] Full ledger mirror (expenses/bills, bank transactions, broader Chart-of-Accounts mapping)
- [ ] GST return filing through Xero
- [ ] Credit notes / refunds
- [ ] Multi-organisation / multi-currency
