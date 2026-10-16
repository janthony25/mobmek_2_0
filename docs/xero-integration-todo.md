# Xero Integration — Implementation Checklist (v1)

Tracks delivery of [`xero-integration-design.md`](./xero-integration-design.md). Section references (§) point into the design doc.

**Status:** Phase 0 code shipped 2026-10-17 (code-complete, not yet live-verified — see `docs/features/xero-integration.md`). Phases ship in order; each leaves the app fully working (sync stays cleanly disabled until Phase 0's connection exists, §2).

---

## Phase 0 — Xero-side setup, OAuth connect flow & config plumbing (§6)

- [ ] Register a Xero developer app at `developer.xero.com` (owner) — note Client ID/secret, set redirect URI(s) — **blocking the live-verification step below, owner action pending**
- [x] Decide + sign off the Xero client dependency — hand-rolled `HttpClient` calls against `login.xero.com`/`identity.xero.com`/`api.xero.com` (no new NuGet dependency, no sign-off friction)
- [x] `XeroConnection` entity (singleton, encrypted token fields via `IDataProtector`) + migration `AddXeroConnection`
- [x] Admin-only "Connect to Xero" settings page: OAuth2 Authorization Code flow, callback endpoint, stores tokens + tenant id — `XeroSettingsPage.tsx`/`XeroCallbackPage.tsx`, `XeroIntegrationController.cs`, gated on new `Permissions.ManageIntegrations`
- [x] Token-refresh hosted job (daily) — `XeroTokenRefreshJob.cs`
- [ ] Create the Xero webhook subscription (owner, against the same app) + store the signing key as a secret (env/user-secrets) — deferred to Phase 2, not needed yet
- [ ] Settings page: default Xero sales account-code dropdown (populated via `accounting.settings.read`) — deferred to Phase 1, not needed yet
- [ ] **Expected output:** clicking "Connect to Xero" completes a real OAuth consent against a real Xero org and `XeroConnection.Status = Connected`; disconnect/reconnect both work; access token auto-refreshes without user action — **not yet run for real; 13 service-level tests pass against a stubbed Xero, but no real Xero org has completed this flow**

---

## Phase 1 — One-way invoice push (§4)

- [ ] `XeroSyncOutbox` entity + `XeroSyncAction` enum + `AppDbContext` config (unique partial index on `InvoiceId` where `Action = 'PushInvoice'`) — migration
- [ ] `Invoice.XeroInvoiceId`/`XeroSyncStatus`/`XeroLastSyncedUtc`/`XeroLastError` + `Customer.XeroContactId` — migration
- [ ] `IXeroClient` / real client: Contacts search/create, Invoices create/update/void, Accounts list (for the settings dropdown)
- [ ] Enqueue hooks: `InvoiceService.GenerateDocumentAsync` (Invoice only) → `PushInvoice`; `RejectAsync` → `VoidInvoice`; no-op when disconnected
- [ ] Contact resolution: match-by-email else create, store `XeroContactId`, reuse on subsequent invoices
- [ ] `XeroSyncJob` (hosted, pattern: `CalendarSyncJob`): invoice/line/GST/account-code mapping per §4 table, idempotent push-or-update, void-blocked-by-payment handling, backoff ladder
- [ ] Service tests with a `FakeXeroClient`: enqueue atomicity, contact match/create/reuse, line/tax mapping, idempotent re-push, void failure surfaced not swallowed, backoff schedule
- [ ] **Expected output:** generating a real invoice in Mobmek produces a real, correctly-totaled `AUTHORISED` invoice in the connected Xero org within ~1 minute; rejecting it voids the Xero copy

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
