# Industry Readiness Gap Analysis

**Date:** 2026-08-03
**Scope:** Whole system except the Cash Flow / finance module (hidden, out of current focus per owner — excluded from this analysis).
**Method:** Read through the actual codebase (entities, controllers, services, frontend pages/deps) rather than assuming from the README, then benchmarked what exists against 2026 industry-standard auto repair shop management platforms (Tekmetric, Shopmonkey, AutoLeap, Fullbay) and NZ-specific competitors (Hoist, Workshop Software, MechanicDesk), via web research (sources at the bottom).

## How to read this doc

Each gap has: what's missing, why it matters (with industry evidence where useful), and a rough effort/impact call. Nothing here is a commitment — it's a menu. Items already tracked in other docs (`email-module-*.md`, `google-calendar-sync-*.md`, `legacy-import-*.md`) are noted as **in progress**, not repeated as new asks.

---

## 1. Current system snapshot

What Mobmek already does well, for context:

- **Core CRM**: Customers → Cars (make/model lookups, VIN, rego) → Jobs, with job items (parts, free-text), labour lines, a service catalog (`JobService`/`JobServiceLine`), multi-mechanic assignment.
- **Appointments**: soft-contact booking (phone-in caller before they're a real customer) that converts to hard Customer/Car/Job links on arrival — a genuinely good design choice for a walk-in-heavy shop.
- **Invoicing/quoting**: snapshot-at-generation money fields (an invoice never silently changes if the job is edited later), sequential document numbers, PDF generation (QuestPDF), GST handling.
- **Reminders & notes**: templated recurring reminders (WOF/service-style) and a sticky-note board.
- **Auth**: ASP.NET Core Identity, cookie auth, lockout after 5 failed attempts, email-confirmation-gated accounts, audit stamping (`UpdatedByUserId`/`UpdatedByName`) on every entity.
- **In flight already** (tracked elsewhere, not re-litigated here): outbound/inbound email (Resend + IMAP mirror), Google Calendar two-way-ish sync for appointments, legacy MSSQL data migration (Phases 0–5 done, Phase 6 cutover pending).

This is a solid single-shop job-card + invoicing system. The gaps below are what separates that from what shops moving off pen-and-paper (or off Tekmetric/Shopmonkey-tier tools) now expect as standard.

---

## 2. Highest-impact gaps (shop-floor revenue & trust)

### 2.1 No Digital Vehicle Inspection (DVI) / photo evidence on jobs
There is currently **no attachment/photo capability anywhere on `Job`, `JobItem`, or `Car`** — the only file-upload code path in the whole system is `TransactionAttachment` (cash-flow receipts, out of scope) and a logo upload on Business Details. A mechanic can't attach a photo of a worn brake pad or a video of a failing belt to a job, and a customer can't be sent that evidence to approve extra work.

This is the single biggest gap relative to the market. Every major 2026 platform (Tekmetric, Shopmonkey, AutoVitals, Torque360, Protractor) treats DVI as core, not an add-on, because the effect on approval rates is large and well documented: shops report visual-proof estimates moving customer approval from roughly 50–60% to 85–90%, and DVI is specifically credited with reducing the "I don't trust this quote" objection that's endemic to the trade.

**What it needs, roughly:**
- Photo/video attachment entity on `Job` (and optionally `JobItem` for "here's the actual failed part"), reusing the existing `IFileStorage` abstraction already built for cash-flow attachments.
- A simple inspection checklist concept (even a flat "point / status (OK, Advise, Fail) / photo / note" list is enough for v1 — doesn't need Tekmetric's full canned-inspection-template engine).
- A customer-facing share link (no login required, expiring token) that renders photos + line items + an Approve/Decline button per line — this is what actually drives the approval-rate lift, not just internal photo storage.
- Ties naturally into the email module already in flight (Phase 2's `EmailComposeService` could send the DVI link) and gives quotations a reason to be sent proactively rather than read out over the phone.

**Effort:** Medium-large (new entity + file handling is done pattern already; the public share-link view is the new surface). **Impact:** High — this is the feature most likely to change close rates and reduce "didn't get a call back" disputes.

### 2.2 No real parts/inventory management
`Product.cs` is explicitly the tutorial/scaffolding entity ("Sample domain entity demonstrating the EF Core + service + controller flow... Replace or extend with the real Mobmek domain model") and isn't wired into jobs at all. The actual parts line on a job, `JobItem`, is **free-text** (`ItemName` typed fresh every time) with a manual trade price and markup — there's no shared parts catalog, no stock-on-hand, no reorder point, no supplier/vendor record, no purchase orders, no barcode scanning, no core-charge tracking.

Every 2026 comparison of shop software lists "Parts & Inventory: vendor integrations, stock levels, cost/markup rules, cores management" as a baseline expectation, not a premium tier.

**What it needs, roughly:**
- Promote `Product` (or a new `Part` entity) into a real catalog: SKU, supplier, cost, stock-on-hand, reorder threshold.
- Let `JobItem` optionally reference a catalog part (still allow free-text for one-off items — don't force every oil filter ever sold into a rigid catalog on day one).
- Low-stock alerts / reorder list as a first cut; full PO workflow and vendor punch-out integrations (WorldPac/NAPA-style) are a later phase, not v1.

**Effort:** Medium. **Impact:** High for shops carrying real stock; lower if parts are always ordered per-job with no shelf stock — worth confirming which model matches how this shop actually operates before building it.

### 2.3 Labour is manual hours×rate with no time-guide or technician time tracking
`Labour` is just `Hours × RatePerHour` (or a flat override) typed in by whoever writes up the job. There's no:
- Standard labour-time reference (what Mitchell1/Alldata provide commercially) to sanity-check quoted hours against a manufacturer-standard time.
- Technician clock-in/clock-out per job, so there's no way to compute **billed hours vs. actual hours** — the single most-watched efficiency metric in the trade (a shop's "technician efficiency %").

**What it needs, roughly:** a simple start/stop timer per `JobMechanic` row would get most of the value (actual time worked vs. billed) without needing a commercial time-guide integration, which is expensive and probably out of scope for a single shop's system.

**Effort:** Small-medium for the timer; large/likely-not-worth-it for a licensed time-guide integration. **Impact:** Medium — mostly a management/reporting win, not customer-facing.

---

## 3. Customer communication & experience

### 3.1 No SMS / two-way texting
Only email exists as a customer channel (and that's mid-build). Grep across the whole codebase turns up no SMS/Twilio integration. Every 2026 competitor treats **unlimited two-way texting** as a baseline included feature (Shopmonkey explicitly bundles it in every plan), because in this trade customers are in cars/at work and don't check email — they read texts. This is arguably a bigger gap than email for reminder/appointment-confirmation purposes, and it's the delivery channel that makes DVI-approval links (2.1) actually get opened.

**Effort:** Medium (Twilio or similar; NZ-specific SMS providers may have better local delivery — worth a small spike before committing). **Impact:** High, and it compounds with the DVI and reminder features already/being built.

### 3.2 No customer self-service portal or online booking
Right now every touchpoint is staff-mediated: a customer can't log in to see past invoices/jobs on their own vehicle, can't approve a quote online without a staff member calling them, and there's no public "book an appointment" page — `AppointmentsPage` is an internal, staff-only calendar. Competing platforms increasingly lead with "customer portal" and "online booking widget" as the first thing a shop's own customers see.

**Effort:** Large (needs a whole separate unauthenticated/lightly-authenticated surface, distinct from staff Identity). **Impact:** Medium-high, but this is a bigger lift than 2.1/3.1 for similar payoff — reasonable to sequence after DVI share-links and SMS, which deliver most of the same "customer feels informed and in control" value more cheaply.

### 3.3 No e-signature capture
No digital signature on quote/job authorization or on vehicle pickup. Low effort relative to value once a DVI share-link view (2.1) exists, since that's the natural place to capture it (tap-to-approve + a signature pad on the same page).

### 3.4 No review/reputation-management nudge
No "invoice marked paid → prompt for a Google review" flow. Small, cheap, and every competitor bundles it. Worth a Phase-5-style deferred item on the email/SMS module rather than its own project.

---

## 4. Vehicle data & NZ-specific compliance

### 4.1 No plate/VIN lookup integration
`Car` requires manual entry of make, model, year, rego, VIN, engine type — every field hand-typed, which is slow and error-prone at the front counter. NZ shops competing with Hoist/Workshop Software increasingly auto-populate this from a rego lookup (Waka Kotahi/NZTA-backed data resellers, e.g. CarJam-style APIs) or VIN decode. This is a quality-of-life/data-accuracy win more than a differentiator, but it's cheap relative to payoff and reduces bad data flowing into the CarMake/CarModel lookups.

**Effort:** Small (one API call at car-creation time, still allow manual override). **Impact:** Medium — mostly speeds up front-counter data entry and cuts typos in Rego/VIN that later cause reminder/lookup mismatches.

### 4.2 Reminders are generic, not WOF/CoF/RUC-aware
`ReminderTemplate` is a flexible, user-defined preset system (good general design), but it doesn't specifically encode NZ's three recurring compliance events shops are expected to track: **WOF** (Warrant of Fitness), **CoF** (Certificate of Fitness, for heavier vehicles), and **RUC** (Road User Charges, for diesel/EV). Competing NZ tools (Hoist, Workshop Software) market WOF-due SMS/email reminders as a named, specific feature — worth seeding these three as default `ReminderTemplate` rows (if not already) and, longer-term, cross-checking due dates against the public NZTA rego/WOF-status data rather than relying purely on shop-entered dates.

**Effort:** Small (seed data) to medium (NZTA data cross-check). **Impact:** Medium, high trust value in NZ market specifically.

### 4.3 No fleet/business-customer model
`Customer` → `Cars` works well for individuals but there's no concept of a business account with many vehicles, consolidated reporting, or fleet-level scheduling. Not urgent unless commercial-fleet customers are a target segment — flagging so it's a conscious choice, not an oversight.

---

## 5. Job workflow depth

### 5.1 No "declined/deferred work" tracking
There's no way to record "we recommended a brake job, customer said not now" as a distinct, trackable line — which is both a lost-upsell-recovery opportunity (surface it again at the next visit) and a liability-protection record ("we did tell them"). This pairs naturally with the DVI feature (2.1): each inspection line just needs an Approved/Declined/Deferred status instead of being all-or-nothing.

### 5.2 No warranty tracking
No warranty period/expiry on `JobItem`/`Labour`, so there's no way to check "is this comeback covered" without manually digging through job history.

### 5.3 `JobStatus` doesn't model the full real-world lifecycle
Currently: `Open, InProgress, AwaitingParts, Completed, Invoiced`. Missing states that most shops track distinctly: **Awaiting Approval** (estimate/DVI sent, waiting on customer) and **Ready for Pickup** (done, customer notified, car still on-site) — both matter once DVI/SMS land, since "waiting on the customer" and "waiting on us" are operationally very different states to see on a job board.

---

## 6. Platform maturity (security, ops, engineering)

These aren't features a customer sees, but they're what "industry ready" means from an operations/risk standpoint — arguably more urgent than several feature gaps above, because failure here is catastrophic rather than merely a missed sale.

### 6.1 Backup & disaster recovery — **highest-priority infra gap**
Postgres runs in a Docker named volume with no evident backup script, retention policy, or restore drill anywhere in the repo. Given real customer data (from the completed legacy MSSQL migration) now lives only in this database, a lost/corrupted volume is a total-data-loss event with no documented recovery path. This should be fixed before Phase 6 legacy cutover goes live, not after.

**What it needs:** scheduled `pg_dump` (or WAL archiving) to off-host storage, a tested restore procedure, and a written retention policy (e.g., daily for 2 weeks, weekly for 3 months).

### 6.2 No CI/CD pipeline
No `.github/workflows` (or equivalent) exists — `dotnet test` (57 backend tests, good coverage habit) and frontend lint/typecheck all run manually, by convention, rather than being enforced by a gate. Nothing stops a change merging with a broken build or a failing test.

### 6.3 Single "Admin" role — no real RBAC
Grepping the whole backend for role usage turns up exactly one role: `Admin`. There's no Manager/Front-desk/Technician tier, which means everyone with a login either sees everything (including margins/profit fields on `JobItem`) or nothing. Most shops don't want a junior tech seeing shop-wide profit numbers, and don't want front-desk staff able to deactivate accounts. Worth at least a "Staff" vs "Admin" split before this scales past a couple of users.

### 6.4 No structured logging or error monitoring
`ILogger` is used in only 4 service files across the whole backend; there's no Serilog/Seq/Sentry/Application Insights wired up. In production, a failing background job (email poll, calendar sync, recurring-transaction posting) or an unhandled exception is currently only visible via `docker compose logs`, if anyone happens to look. No alerting exists for "the email poll job has been failing for 3 days."

### 6.5 No health-check endpoint or rate limiting
No `/health` endpoint for uptime monitoring, and no ASP.NET Core rate-limiting middleware — login lockout (5 attempts/15 min) is good, but there's nothing throttling the API generally, which matters more once this is reachable from the public internet rather than a LAN.

### 6.6 Frontend has zero automated tests
57 xUnit tests cover the backend service layer well, but the React frontend (`mobmek_frontend/src`) has no `.test.*`/`.spec.*` files at all. CRUD flows, form validation, and the invoice/PDF print path are only verified by hand.

### 6.7 No global/cross-entity search
There's no single search box to find "that customer who called, I think their name was Dave, driving a white Hilux" by name/phone/rego across Customers/Cars/Jobs at once — every lookup page (`CustomersPage`, `CarDetailPage`, etc.) is scoped to its own entity. This is a small build but a constant, daily friction point at a front counter fielding phone calls.

---

## 7. Suggested sequencing

Not a schedule — a rough "if you had to pick order" based on impact vs. effort, respecting that finance is out of scope and that email/calendar/legacy-cutover are already committed work:

1. **Now / before Phase 6 legacy cutover:** backup & restore for Postgres (6.1) — this is the one item where delay carries real risk to data already migrated in.
2. **Next, feature-side:** DVI photo capture + customer share-link (2.1) and SMS (3.1) — these two compound with each other and with the in-flight email module, and are the biggest lever on customer trust/close rate.
3. **Alongside, ops-side:** basic RBAC split (6.3), structured logging + a health endpoint (6.4/6.5), and a minimal CI gate (6.2) — cheap, and every week without them is a week of accumulated risk.
4. **Then:** parts/inventory catalog (2.2), declined-work tracking + `JobStatus` additions (5.1/5.3) — natural follow-ons once DVI exists, since DVI is what generates "declined" lines in the first place.
5. **Later:** technician time tracking (2.3), plate/VIN lookup (4.1), WOF/CoF/RUC-specific reminder seeding (4.2), global search (6.7), e-signature (3.3), review-request nudges (3.4).
6. **Bigger, standalone projects, only if the business direction calls for them:** customer self-service portal + online booking (3.2), fleet/business-customer model (4.3), frontend test suite build-out (6.6), labour time-guide integration (2.3's harder half).

---

## Sources

- [Best Auto Repair Shop Management Software (2026): Top Choices Compared — Shopmonkey](https://www.shopmonkey.io/blog/best-auto-repair-shop-management-software-2026-top-picks-compared)
- [Compare Shopmonkey vs Tekmetric 2026 — Capterra](https://www.capterra.com/compare/169022-190952/Shopmonkey-vs-Tekmetric)
- [7 Best Auto Repair Shop Management Software 2026 (With Inventory & DVI)](https://techroute66.com/auto-repair-management-software)
- [Best Auto Repair Shop Software in 2026: Honest Comparison of 12 Platforms — Eligant](https://www.eligantauto.com/en/resources/blog/best-auto-repair-shop-software-2026-comparison)
- [How Digital Vehicle Inspection Increases Approvals — Torque360](https://blog.torque360.co/how-digital-vehicle-inspection-increases-approvals/)
- [Digital Vehicle Inspections: The Complete Guide — Workshop Software](https://workshopsoftware.com/knowledge-base/digital-vehicle-inspections/)
- [What Are Digital Vehicle Inspections & How Do They Work? — AutoLeap](https://autoleap.com/blog/everything-you-need-to-know-about-digital-vehicle-inspections/)
- [Digital Vehicle Inspection in Auto Repair: What It Is and Why It Matters — Autorox](https://www.autorox.ai/post/digital-vehicle-inspection-auto-repair-workshops)
- [WOF Due Reminder for New Zealand Workshops — Workshop Software](https://workshopsoftware.com/wof-due-reminder/)
- [Hoist — Workshop Management Software for NZ Mechanics](https://hoist.nz/)
- [Best Workshop Software NZ 2026 Comparison — Hoist](https://hoist.nz/blog/best-workshop-management-software-new-zealand)
