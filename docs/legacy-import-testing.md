# Legacy Import — Manual Testing Walkthrough

Owner checklist for verifying the imported legacy data in the running app, at your own pace. Backs the sign-off items in [`legacy-import-design.md`](./legacy-import-design.md) §6 and Phase 6 of [`legacy-import-todo.md`](./legacy-import-todo.md).

**State as of 2026-07-10:** Phases 0–5 ran for real — all legacy customers, cars, jobs, invoices, quotations and appointments are already in the dev Postgres DB. Nothing needs to be re-run before testing. Dev data coexists with imported data until the Phase 6 wipe.

---

## 0. Getting the stack running (only after a reboot / cold start)

If the app is already up, skip to §1.

```bash
colima start                      # docker engine does NOT auto-start after reboot
cd /Users/jun/mobmek_2_0
docker compose up -d              # db + api + frontend
```

- App: http://localhost:3000 · Swagger: http://localhost:8080/swagger
- Login: `justforvalo25@gmail.com` / `DevAdmin!2026`
- The legacy MSSQL container is **not** needed for browser testing — only for re-running the importer: `docker compose --profile legacy up -d`
- If compose can't reach the docker daemon → you forgot `colima start`

---

## 1. Sign-off checklist (in the browser)

Work through these in any order; tick as you go.

### Customers & cars

- [ ] Customers list shows ~453 rows (447 imported + dev ones); names recognizable from the old system
- [ ] Open a legacy customer → their cars, jobs, invoices and quotations are all visible
- [ ] Imported customer's provenance note reads "Imported from legacy system (Customer #…)"
- [ ] Edit an imported customer (e.g. fix a name split or an `N/A` phone) → saves normally
- [ ] Add a **new** car to an imported customer → works like native data

### Jobs

- [ ] Open an imported job → items, labour and service lines all present, totals match the old system
- [ ] The "Imported details" block in job notes shows Issue / dates / labour names / mechanic where the old job had them
- [ ] Synthetic jobs (created to carry old invoices/quotations) show title = the document's issue name and the note "Auto-created during legacy import for invoice/quotation #…" — their own totals are 0 by design; the money lives on the document
- [ ] Add a **new** job to an imported customer/car → works
- [ ] Edit an imported job (e.g. change labour hours) → totals recompute, save works

### Invoices & quotations

- [ ] Open an imported invoice you have an old printout for → items, subtotal, GST, discount, shipping, total all match
      ⚠️ The **printed number will differ** from the old system (renumbered chronologically: imported invoices are INV-0017…0569, quotations QUO-0006…0177; dev docs kept 1–16 / 1–5). Old ↔ new id cross-reference is in the `legacy_import_map` table and the import report.
- [ ] A paid imported invoice shows paid state, amount, date and payment mode
- [ ] Mark an **unpaid imported** invoice as paid → works like a native one
- [ ] **Email/PDF:** email an imported invoice **to your own address** (this really sends via Resend) → PDF attachment renders the imported data correctly ← *the one check not yet done by tooling*
- [ ] Rejected imported invoices/quotations (63 + 9) show status Rejected
- [ ] Generate a **new** invoice on an imported job → numbering continues after 569
- [ ] An accepted old quotation (2 exist) shows "[Accepted in legacy system]" in its notes

### Appointments

- [ ] Calendar renders imported appointments (192 in 2026, 66 in 2025, 1 in 2024)
- [ ] Open a linked appointment → its customer, car and job resolve correctly
- [ ] Cancelled ones (7) show as Cancelled; old "In-Progress" ones show as Arrived
- [ ] Statuses/durations look sane — 23 appointments had broken end times in the old data and were defaulted to 1 hour (listed in the report)

---

## 2. Review the flags worklist

Open the latest `mobmek_api/legacy-import-report-<timestamp>.md` (gitignored — contains customer data). Current: `legacy-import-report-20260710-013322.md`.

The **Reconciliation** table must be all ✅ (it is — 18 rows). The **Flags** section is your manual-cleanup list, best done in the UI as you test:

| Flag | Count | What to do |
|---|---|---|
| suspected-duplicate-customer | 30 | Review pairs; merge/delete by hand if truly duplicates |
| single-word-name | 170 | Old single-word names became `LastName = "-"` — fix the ones you care about |
| placeholder-phone | 20 | Phone imported as `N/A` — fill in real numbers |
| duplicate-rego | 6 | Same rego on two cars — decide which is real |
| appointment-end-adjusted | 23 | End time was at/before start (AM/PM slips) → defaulted to +1 h |
| item-name-truncated | 1 | QuotationItem #96 was 1,838 chars; full original text is preserved in the report |
| job-status-mapped | 20 | Old "Waiting Customer" jobs imported as In Progress |
| service-reused | 2 | Legacy "Full Service" price 250 vs catalog 200 — confirm catalog price |

---

## 3. Terminal checks (optional, anytime)

```bash
cd /Users/jun/mobmek_2_0/mobmek_api
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"

dotnet test          # full suite (420 green as of 2026-07-10)

# Importer dry-run: connects to both DBs, skips everything already imported,
# rolls back, prints a fresh report. Safe to run whenever.
# (needs the legacy container: docker compose --profile legacy up -d)
dotnet run --project src/MobmekApi.LegacyImport -- --dry-run
```

The importer is idempotent — a real re-run (`dotnet run --project src/MobmekApi.LegacyImport`) also just skips all mapped rows.

---

## 4. Resetting the DB / getting the legacy data back

What each teardown actually loses:

| Command | Postgres (imported + test data) | Restored legacy MSSQL | `.bak` file |
|---|---|---|---|
| `docker compose stop` / `start` | kept | kept | kept |
| `docker compose down` | **kept** (named volume) | **lost** (lives in the container, no volume) | kept (host folder `legacy-backup/`) |
| `docker compose down -v` | **lost** | **lost** | kept |

So `down -v` is the "wipe the test data" button — afterwards you rebuild a clean, legacy-only system like this:

```bash
cd /Users/jun/mobmek_2_0

# 1. Everything up, including the legacy profile. Postgres starts empty;
#    the API auto-applies migrations and re-seeds (admin login, car makes/models) on startup.
docker compose --profile legacy up -d

# 2. Wait for SQL Server (~30–60 s under Rosetta), then re-restore the .bak:
docker logs mobmek_legacy_mssql 2>&1 | grep -c "ready for client connections"   # want ≥ 1

docker exec mobmek_legacy_mssql /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P 'LegacyImport!2026' -Q "
RESTORE DATABASE MobileMekaniko
FROM DISK='/var/opt/mssql/backup/db_aae44c_mobmekv200_7_9_2026_16.bak'
WITH MOVE 'db_aae44c_jcalupcupan_Data' TO '/var/opt/mssql/data/MobileMekaniko.mdf',
     MOVE 'db_aae44c_jcalupcupan_Log'  TO '/var/opt/mssql/data/MobileMekaniko_log.ldf'"

# 3. Confirm the API is up (it must have run migrations before the importer writes):
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:8080/swagger/index.html   # 200

# 4. Re-run the importer — the map table was wiped too, so this is a full fresh import.
cd mobmek_api
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"
dotnet run --project src/MobmekApi.LegacyImport -- --dry-run   # optional preview
dotnet run --project src/MobmekApi.LegacyImport
```

Result: a legacy-only database — no dummy data, invoice numbering starts at INV-0001 / QUO-0001 (matching what the final cutover will look like).

Also gone after `down -v`, so plan to redo:
- **You'll be logged out everywhere** (data-protection keys volume wiped) — just log in again; the admin account is re-seeded from compose env
- **Settings entered through the UI** (business details, email settings, GST setting, reminder templates …) — re-enter them; the Resend API key itself is fine (comes from `.env`)

After a plain `down` (no `-v`): only steps 1–2 are needed — Postgres kept everything, you just need the MSSQL restore back if you want to run the importer again.

---

## 5. When testing is done → Phase 6 cutover

Everything above green means the import logic is trusted. Cutover (see todo Phase 6) is then:

1. Old system stops taking new work; get a **fresh `.bak`** from the host → replace `legacy-backup/…` → re-restore
2. **Wipe the dev data** from the target DB (dummy customers/jobs/invoices go; imported numbering then starts at 1)
3. Full `--dry-run` → review flags → real run → keep the final report
4. Repeat this checklist once more against the real cutover data → sign off
