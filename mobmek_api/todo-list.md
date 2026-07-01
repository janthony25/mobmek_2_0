# Invoice API — Fix & Align with Reference (`MobileMekaniko-Business`)

Goal: make invoice generation work correctly and bring over the reference's proven
"create invoice from a job" behaviour, **keeping our differences**: snapshot model,
a separate centralized `GstSetting` entity, Guid IDs, `DateOnly` dates, and the strict
layered architecture (all logic in `InvoiceService`, never the controller).

Scope: **API only.** No front end. No PDF/email in this pass (tracked as a later phase).

---

## How the reference creates an invoice from a job (analysis)

Reference class: `NewInvoice` / `NewInvoiceItem` (the job-linked one; the older `Invoice`
is customer-linked and not relevant). Flow lives in `NewInvoiceController.CreateInvoice`:

1. Load the job with its details (`GetJobDetailsAsync`): customer, car, job items, labour, services.
2. Compute totals (`CalculateTotals`): `subTotal = items + labour + services`, then
   **`taxAmount = subTotal * 0.15` and `totalAmount = subTotal + taxAmount`** (GST **added on top**, hardcoded 15%).
3. Persist a `NewInvoice` header snapshotting: `IssueName` (job issue), `Notes` (job notes),
   `LabourPrice`, `SubTotal`, `TaxAmount`, `TotalAmount`, `DueDate`, `PaymentTerm`, `ModeOfPayment`,
   `Discount = 0`, `ShippingFee = 0`, `Status = "Active"`, `IsPaid = false`.
4. Persist line items: **one per job item**, plus a single **"Labour"** line.
   ⚠️ Reference does **not** persist service lines as items (services only feed the total and the PDF).
5. Lifecycle endpoints: `MarkAsPaid`, `SendInvoiceEmail`, `Reject` (soft — `Status = "Rejected"`).
   Payment analytics: `CashAmount` / `CardAmount`, parsed out of a `ModeOfPayment` string via regex.

### What we do differently (keep these)
- **Snapshot money fields** frozen at generation (already done) — good, keep.
- **Separate `GstSetting` entity** supplies the rate instead of a hardcoded `0.15` — keep and use it.
- **Service lines ARE snapshotted** as their own invoice items (more complete than the reference) — keep.
- Guid IDs, `DateOnly DueDate`, `[ApiController]` REST routes, service-layer logic — keep.

---

## Decisions (confirmed)
- [x] **GST is added on top**: `TaxAmount = SubTotal * GstRate`, `TotalAmount = SubTotal + TaxAmount`.
      Rate comes from our `GstSetting` entity (snapshotted onto the invoice as `GstRate`).
- [x] **Add the full payment lifecycle** (mark-as-paid, amount paid, date paid, cash/card split).

## Decisions made during implementation
- [x] Cash/card split: `MarkInvoicePaidRequest` accepts explicit `CashAmount` + `CardAmount`.
      Recorded as-supplied for analytics, alongside (not replacing) `ModeOfPayment`.
- [x] `ModeOfPayment` and `PaymentTerm` moved from `CreateInvoiceRequest` to `MarkInvoicePaidRequest` —
      both are only actually known once the customer pays, not at invoice generation.
      `CreateInvoiceRequest` now only carries `DueDate`. Entity columns unchanged (already nullable).
- [x] `MarkPaidAsync` assumes **full payment**: `AmountPaid = TotalAmount`; cash/card are recorded
      but NOT validated to sum to the total. A rejected invoice can't be paid (returns `null`).
- [x] A job can still have **many** invoices — generating a new one is not blocked by an existing paid/rejected one.

## Decisions still open (revisit if the business needs them)
- [ ] Partial-payment support (`AmountPaid < TotalAmount`, multiple payments) — currently full-payment only.
- [ ] Distinct 409/Conflict response when paying a rejected/already-paid invoice (currently folded into 404).
- [ ] Whether to cap active invoices per job (e.g. one non-rejected invoice at a time).

---

## THE BUG (do first)

- [x] **Fix totals in `InvoiceService.GenerateAsync`.** Currently `TotalAmount = subTotal` (GST never added),
      so every total is understated by the tax. Change to:
      `taxAmount = Round(subTotal * gstRate)` and `TotalAmount = Round(subTotal + taxAmount)`.
      File: `src/MobmekApi/Services/InvoiceService.cs:46-68`.
- [x] Update/extend `InvoiceServiceTests` to assert the new total (`subTotal + tax`) and the snapshotted `GstRate`.

---

## Implementation checklist

### 1. Entity — `Invoice` (add payment-lifecycle fields) ✅
File: `src/MobmekApi/Entities/Invoice.cs`
- [x] `bool IsPaid` (default `false`)
- [x] `decimal? AmountPaid`
- [x] `DateOnly? DatePaid`
- [x] `decimal? CashAmount`
- [x] `decimal? CardAmount`
- [x] EF precision config for new decimals in `AppDbContext` (`numeric(18,2)`)
- [~] (optional) `bool IsEmailSent` — skipped; deferred to the email phase.

### 2. DTOs ✅
File: `src/MobmekApi/DTOs/InvoiceDtos.cs`
- [x] Add the new fields to `InvoiceDto` (read side).
- [x] New `MarkInvoicePaidRequest(decimal? CashAmount, decimal? CardAmount, DateOnly? DatePaid)`.
- [x] Confirm `CreateInvoiceRequest` still only carries `DueDate`, `ModeOfPayment`, `PaymentTerm`
      (lines/totals stay auto-built from the job — do NOT let the client send amounts).

### 3. Service — `IInvoiceService` / `InvoiceService` ✅
Files: `src/MobmekApi/Services/IInvoiceService.cs`, `InvoiceService.cs`
- [x] Fix `GenerateAsync` totals (see THE BUG).
- [x] Set `IsPaid = false`, null payment fields at generation (via entity defaults).
- [x] New `Task<InvoiceDto?> MarkPaidAsync(Guid jobId, Guid id, MarkInvoicePaidRequest request, CancellationToken)`:
      set `IsPaid = true`, `DatePaid` (defaults today), `AmountPaid = TotalAmount`, `CashAmount`, `CardAmount`;
      returns `null` when there's no payable (`Active`) invoice with that id on the job — i.e. not found **or** rejected.
- [x] Update `ToDto` to project the new fields.
- [x] Keep `AsNoTracking()` on reads, thread `CancellationToken`, entities stay inside the service.

### 4. Controller — `InvoicesController` ✅
File: `src/MobmekApi/Controllers/InvoicesController.cs`
- [x] `POST api/jobs/{jobId}/invoices/{id}/pay` → `MarkPaidAsync` (thin: `null` → 404, else 200).
      (Reference names it `MarkAsPaid`; use REST `/pay`.)
- [x] `[ProducesResponseType]` attributes for Swagger; no business logic in the controller.

### 5. Migration ✅
- [x] `dotnet dotnet-ef migrations add AddInvoicePaymentLifecycle --project src/MobmekApi`
      (PATH gotcha: `export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"` first).
      → `20260701024238_AddInvoicePaymentLifecycle`.
- [x] Sanity-check the generated migration (4 nullable columns + `IsPaid` boolean default `false`).

### 6. Tests (`tests/MobmekApi.Tests/Services/InvoiceServiceTests.cs`) ✅
- [x] `GenerateAsync` total = `subTotal + tax`, with `GstRate` snapshotted from `GstSetting`.
- [x] `GenerateAsync` snapshots items + a Labour line + service lines (already covered — keep).
- [x] `GenerateAsync` leaves the invoice unpaid (all payment fields null / `IsPaid` false).
- [x] `MarkPaidAsync` happy path stamps `IsPaid`, `DatePaid`, `AmountPaid`, cash/card.
- [x] `MarkPaidAsync` defaults `DatePaid` to today when omitted.
- [x] `MarkPaidAsync` returns `null` for an invoice on a different job / missing id.
- [x] `RejectAsync` still soft-rejects; a rejected invoice can't be marked paid.
- [x] `dotnet test` green before calling it done (127 passed).

---

## Later phases (not this pass)
- [ ] PDF generation from a saved invoice (reference: `NewInvoicePdfService`).
- [ ] Email invoice to customer + `IsEmailSent` flag (reference: `SendInvoiceEmail` / `EmailPdfService`).
- [ ] Paginated / filtered invoice lists (paid / unpaid / unsent) — reference has these; add if the UI needs them.
- [ ] Analytics rollups from `CashAmount` / `CardAmount`.
