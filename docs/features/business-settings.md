# Business Settings

**Last verified:** 2026-10-07 (read against `Controllers/BusinessDetailsController.cs`, `Entities/BusinessDetails.cs`, `DTOs/BusinessDetailsDtos.cs`, `mobmek_frontend/src/pages/BusinessDetailsSettingsPage.tsx`)

Covers the single business-profile record used on invoices/emails (company info, GST number, bank details, logo).

## Status summary

| Sub-capability | Status |
|---|---|
| Company profile fields (name, address, phone, email, GST number, website, bank details) | Working |
| Logo upload/replace/remove | Working |
| Invoice numbering prefix / format | **Does not exist** |

## Details

### Company profile & logo — Working
Singleton entity (`Entities/BusinessDetails.cs:7-30`): `Name`, `Address`, `Email`, `BusinessPhone`, `Telephone`, `GstNumber`, `Website`, `BankDetails`, plus logo (`LogoStorageKey`/`LogoFileName`/`LogoContentType`). `BusinessDetailsController`: `GET`/`PUT`, plus `POST`/`GET`/`DELETE` for the logo (5MB cap, must be `image/*`). Read is open to any signed-in staff; writes gated by `Permissions.ManageBusinessSettings`. `BusinessDetailsSettingsPage.tsx` is a complete form covering every field, with a confirm-dialog before save and an `UpdatedByTag` audit display.

### Invoice numbering prefix — does not exist
There is no settings field for this anywhere. Invoice/quote numbers are generated from a hardcoded format string baked into `Services/InvoiceService.cs` at three call sites (`$"{(DocumentType=="Quotation"?"QUO":"INV")}-{SequenceNumber:D4}"`). If a per-shop configurable prefix is wanted, it needs a new field on `BusinessDetails` (or a dedicated settings entity) plus a code change in `InvoiceService` — it is a real gap, not a UI oversight; there's nowhere to even put the value today.

## Recommended follow-ups
- Add a configurable invoice/quote number prefix if the business ever needs one (e.g. multi-location, rebrand).
