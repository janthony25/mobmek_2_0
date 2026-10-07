# Invoices, Quotes & Public Booking

**Last verified:** 2026-10-07 (read against `Controllers/{Invoices,PublicBooking}Controller.cs`, `Services/{Invoice,InvoicePdf,OutboundEmail,PublicBooking}Service.cs`, `Entities/Invoice.cs`, migrations, `mobmek_frontend/src/pages/{Invoices,Quotations}Page.tsx`, `components/invoices/*`, test suite; PDF-unification and sequence-number concurrency fixes shipped same day, live-verified)

## Status summary

| Sub-capability | Status | Key file(s) |
|---|---|---|
| Invoice vs Quote data model | Working (single entity + flag) | `Entities/Invoice.cs:30-34` |
| Invoice numbering | Working, concurrency-safe | `Services/InvoiceService.cs` (`SaveWithUniqueSequenceNumberAsync`) |
| GST/tax calc on invoices | Working | `Services/InvoiceService.cs` |
| Payment status tracking + ledger posting | Working | `Services/InvoiceService.cs:316-431` |
| Invoice PDF generation | Working — single renderer | `Services/InvoicePdfService.cs`, `GET /api/jobs/{jobId}/invoices/{id}/pdf` |
| Send Email (list + customer pages) | Working | `OutboundEmailService.cs`, `ResendEmailSender.cs` |
| Public Booking backend | Working, tested | `Controllers/PublicBookingController.cs`, `Services/PublicBookingService.cs` |
| Public Booking frontend | **Missing — no consuming UI anywhere (deliberately deferred)** | — |

## Details

### Invoice vs Quote — single entity, not separate tables
`Invoice.DocumentType` (`"Invoice"`/`"Quotation"`) + `Status` (`"Active"`/`"Rejected"`/`"Accepted"`). `InvoiceService.GenerateDocumentAsync` (lines 145-238) is the shared path for both, differing only in `DocumentType` and due-date policy (quotation = issue+30 days, hardcoded). `AcceptQuotationAsync` (lines 240-293) converts a quotation into a new Invoice row by copying its snapshotted lines; the original quotation is kept, flipped to `Accepted`. `MarkPaidAsync` explicitly rejects quotations (line 324) — structurally non-payable.

### Invoice numbering — Working, concurrency-safe (fixed 2026-10-07)
`SequenceNumber` = `MAX(SequenceNumber) WHERE DocumentType=X) + 1`, counted independently per doc type. **Fixed:** a unique index on `(DocumentType, SequenceNumber)` now backs this at the DB level (migration `AddInvoiceSequenceNumberUniqueIndex`, confirmed no pre-existing duplicates before adding it), and both generation call sites (`GenerateDocumentAsync`, `AcceptQuotationAsync`) go through a new shared `SaveWithUniqueSequenceNumberAsync` helper that retries (up to 5 attempts) with a freshly recomputed number whenever a concurrent generation wins the race first — detected via `Npgsql.PostgresException.SqlState == UniqueViolation`. Live-verified: generated a real quotation post-fix, got the correctly-continued `QUO-0183`.

Prefix is configurable (fixed 2026-10-07) — see `business-settings.md` ("Invoice numbering prefix"); the numeric sequence itself is still a fixed 4-digit zero-padded format.

### GST calc — Working
Pulls the live GST rate at generation time and **snapshots** it onto the invoice (`GstRate`/`TaxAmount`) — later global rate changes don't retroactively affect issued invoices (tested). GST treated as tax-inclusive, applied on discounted subtotal.

### Payment tracking + ledger — Working
`MarkPaidAsync` stamps paid fields and posts a real cash-flow ledger entry routed to configured cash accounts by cash/card split, with guards against archived/missing accounts. `RejectAsync` reverses ledger postings. Heavily tested (34+ tests covering routing, idempotency, rejection cleanup).

### Invoice PDF — Working, single renderer (fixed 2026-10-07)
Previously two separate, non-overlapping implementations existed: the real server-side QuestPDF renderer (`InvoicePdfService.cs`, only ever invoked from the email-attachment path) and a completely separate `InvoicePrintPage.tsx` that re-fetched JSON, rendered its own HTML, and called `window.print()` — what "View/Download PDF" actually showed. Users never saw the QuestPDF output; the two could drift.

**Fixed:** `InvoicePrintPage.tsx` is deleted, and its route removed from `App.tsx`. `InvoicesController` gained `GET /api/jobs/{jobId}/invoices/{id}/pdf` (`?download=true` toggles `Content-Disposition: attachment` vs inline), backed by the same `IInvoicePdfService.GenerateAsync` the email path already used — one renderer now, used everywhere. All four frontend call sites (`DocumentListPage.tsx`, `InvoicesSection.tsx`, `QuotationsSection.tsx`, `CustomerDetailPage.tsx`) repointed at the new endpoint via the pre-existing (previously unused) `apiUrl()` helper in `api/client.ts`; the redundant third "Print" menu item (a small popup that relied on the old page's auto-print JS, meaningless against a raw PDF) was dropped from `InvoicesSection.tsx`/`QuotationsSection.tsx` for consistency with the other two call sites' two-action (View/Download) pattern. Live-verified via curl: inline response has no `Content-Disposition` header and renders as a valid 1-page PDF; `?download=true` adds `Content-Disposition: attachment; filename=INV-0586.pdf` — both confirmed real PDF bytes.

### Send Email — Working, fully traced end to end
`EmailComposeModal` → `sendInvoiceEmail` → `POST /api/jobs/{jobId}/invoices/{id}/email` → `InvoicesController.SendEmail` → `OutboundEmailService.SendInvoiceEmailAsync` → composes HTML + builds a real PDF attachment (via `InvoicePdfService`) → writes a `Queued` audit row → `ResendEmailSender` makes a real HTTP POST to `api.resend.com`, retries once on 429/5xx → row updated to `Sent`/`Failed`. A background `OutboundStatusPollJob` later upgrades status to `Delivered`/`Bounced`/`Complained` via a one-way rank so a late "Delivered" can't overwrite a "Bounced". Gated on `ResendConfigured`; shows a clear "not configured" message otherwise. See `email-module.md` for the shared pipeline detail. Used identically from both the global invoice/quote lists and the customer detail page.

### Public Booking — backend real, frontend doesn't exist
`PublicBookingController.cs`: `[AllowAnonymous]`, origin-allowlisted CORS, explicit rate-limit policies. `PublicBookingService.cs` is a genuine NZ-timezone-aware (`Pacific/Auckland`) slot engine: 30-min slot grid, 60-min appointments, 8-week horizon, Sunday-closed, DST-safe, rejects double-booking/past-slots/out-of-horizon, enqueues a calendar-sync item on success. 19 tests covering edge cases.

**But `grep` across the entire frontend for `public/booking`/`PublicBooking` returns nothing — no route, no component, no API module anywhere, and no separate marketing-site repo exists.** The backend shipped 2026-08-07, after the industry-readiness doc (2026-08-04) flagged "no public booking page" as a gap — so the doc is now stale on the backend point, but still correct that there's **no customer-visible way to use this feature.**

**Decision (2026-10-07): deliberately deferred.** Building a public, unauthenticated booking page is a real scoping exercise (standalone route in this app vs. a separate site, branding, which fields to collect) — not a quick fix. Left as a documented gap for a dedicated session.

## Recommended follow-ups
- ~~Decide whether "View/Download PDF" should serve the QuestPDF document~~ — done 2026-10-07, one renderer now.
- ~~Decide whether invoice-numbering concurrency is worth fixing~~ — done 2026-10-07, unique index + retry.
- Build a public-facing booking page when there's time to scope it properly — the backend is sitting there fully tested and unused.
