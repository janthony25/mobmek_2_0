# Business Settings

**Last verified:** 2026-10-07 (read against `Controllers/BusinessDetailsController.cs`, `Entities/BusinessDetails.cs`, `DTOs/BusinessDetailsDtos.cs`, `Services/InvoiceService.cs`, `Services/InvoicePdfService.cs`, `Services/EmailComposeService.cs`, `mobmek_frontend/src/pages/BusinessDetailsSettingsPage.tsx`)

Covers the single business-profile record used on invoices/emails (company info, GST number, bank details, logo, invoice/quote numbering prefix).

## Status summary

| Sub-capability | Status |
|---|---|
| Company profile fields (name, address, phone, email, GST number, website, bank details) | Working |
| Logo upload/replace/remove | Working |
| Invoice numbering prefix / format | Working |

## Details

### Company profile & logo — Working
Singleton entity (`Entities/BusinessDetails.cs:7-33`): `Name`, `Address`, `Email`, `BusinessPhone`, `Telephone`, `GstNumber`, `Website`, `BankDetails`, `InvoicePrefix`/`QuotePrefix`, plus logo (`LogoStorageKey`/`LogoFileName`/`LogoContentType`). `BusinessDetailsController`: `GET`/`PUT`, plus `POST`/`GET`/`DELETE` for the logo (5MB cap, must be `image/*`). Read is open to any signed-in staff; writes gated by `Permissions.ManageBusinessSettings`. `BusinessDetailsSettingsPage.tsx` is a complete form covering every field, with a confirm-dialog before save and an `UpdatedByTag` audit display.

**Bug fixed 2026-10-07:** the logo's "Remove" button called `deleteBusinessLogo` straight from `onClick` with zero confirmation — the page already had a `ConfirmDialog` for the unrelated "Save" action, which masked the fact logo removal bypassed one entirely. Found via an app-wide audit triggered by a user-reported instant-delete bug on job photos (see `jobs-workshop-floor.md`). Now gated behind its own `ConfirmDialog` (`confirmingLogoRemove` state). Not live-verified on a real device this session — type-checked and linted clean.

### Invoice numbering prefix — Working (fixed 2026-10-07)
`BusinessDetails.InvoicePrefix`/`QuotePrefix` (default `"INV"`/`"QUO"`, server-validated `^[A-Z0-9]{1,10}$` via `UpdateBusinessDetailsRequest`) now feed every place a document number is formatted: `InvoiceService.FormatDocumentNumber` (list/detail DTOs + the cash-ledger posting description), `InvoicePdfService.GenerateAsync` (PDF header/filename), and `EmailComposeService.ComposeInvoiceEmailAsync` (subject line). `BusinessDetailsSettingsPage.tsx` exposes both as required fields next to the rest of the letterhead. Migration `AddInvoiceQuotePrefixToBusinessDetails`. Covered by `InvoiceServiceTests.GenerateAsync_AndGenerateQuotationAsync_UseConfiguredPrefixes` and updated `BusinessDetailsServiceTests`.

Live-verified via curl against the dev stack post-rebuild: `GET /api/business-details` returns `invoicePrefix:"INV"`, `quotePrefix:"QUO"` for the pre-existing row. **Caught in that same check:** the EF-generated migration initially used `defaultValue: ""` for the `ADD COLUMN` (EF doesn't read C# property initializers for `AddColumn` scaffolding), which would have backfilled every existing `BusinessDetails` row with an empty prefix instead of `"INV"`/`"QUO"`. Fixed by hand-editing the migration's default values and re-running it against the dev DB before rebuilding.

## Recommended follow-ups
None open for this file.
