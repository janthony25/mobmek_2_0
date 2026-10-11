# Legacy Import — Duplicate Customer Review

Manual-review worklist for the `suspected-duplicate-customer` flags raised by the
2026-10-11 Phase 6 cutover import (`mobmek_api/legacy-import-report-20261011-080122.md`).
These were **flagged, not auto-merged** — check each box once you've looked at it in the
app and merged/fixed/left-alone as appropriate.

Legacy IDs are the old system's `CustomerId`; look them up via the `legacy_import_map`
table or just search by name/phone in the new UI.

## High confidence (identical or near-identical phone/email — likely the same person)

- [ ] `#35, #2107, #2380` — NZ BUILD GROUP / EQUIPT BUILDING SUPPLIES LTD — same phone `021521556` + email `troy.webber@nzbg.co.nz` on all 3; `#35` and `#2107` even share rego `MEY543`
- [X] `#2239, #2240, #2241, #2242` — TOMU MAKAATI ×4 — same phone `02102925552` on all 4
- [X] `#2329, #2330` — BIANCA TUISAMOA ×2 — same phone `021652716` *and* same rego `PEB332`
- [X] `#2425, #2426` — QSH23 / QSH23 — same phone `0210633833` + email `sshansia@hotmail.co.nz`; rego used as the name
- [X] `#2525, #2552` — MOSES / HONDA ODESSEY — same phone `021460256` + email `mtpt8085@gmail.com`; second name is a car model, not a person
- [X] `#2468, #2469` — JOSEPH GILLIARD / JOSEPH GILLIARD — phone `0204210773` vs `020410773`, email `gilliardjoseph70@...` vs `gillioardjoseph70@...` (one digit/letter off both times)
- [X] `#2423, #2428` — GWM / RYZ — same phone `021305225` + email `ryz_is@icloud.com`
- [ ] `#2365, #2413` — JEREMIAH PAEA / JOVEN — same phone `02109185235` + email `jpaeulberg98@...` under two different first names
- [X] `#2159, #2278` — AARON / AARON — same phone `0221529977`
- [X] `#2216, #2217` — BETH / BETH — same phone `02102931717`
- [X] `#2466, #2467` — NORBEN / NORBEN — same phone `02102056842`
- [X] `#2473, #2474` — TJ / TJ — same phone `0204816606`
- [X] `#2319, #2332` — JUNIOR UTUMAPU ×2 — same phone `02109126483`
- [X] `#2326, #2327` — JENAL LEONE ×2 — same phone `02108035324`
- [X] `#2268, #2269` — ANEMESH THAPA ×2 — same phone `0225062889` + email
- [X] `#2503, #2509` — AMITH ×2 — same phone `0275210091`
- [X] `#2512, #2520` — BRADEN ×2 — same phone `0225110400` + email `bradenkuan@...`
- [X] `#2162, #2163` — Mike Err ×2 — same phone `02102721236`
- [ ] `#2397, #2414` — CECILIA / CECILLA — same phone `0210544529`, spelling variant of same name - NOTE COME BACK LATER
- [X] `#2317, #2318` — DIANA / DIANA TUI — same phone `0279403357`, surname added second time

## Medium confidence (shared phone, different-enough names — worth a look)

- [ ] `#2308, #2367, #2530` — ALBERTO BISCO / RR / RR — same phone `0220676291` on all 3, could be a shared/business line - NOTE COME BACK LATER
- [X] `#2372, #2376` — JHOVAN / JOVAN — same phone `0211544435`, likely spelling variant
- [X] `#2485, #2521` — SHANE / SHEEN — same phone `02102299935`, likely typo of one name
- [X] `#2259, #2546` — TOMY / TOMAS — same phone `0226387948`, likely nickname vs full name
- [X] `#2290, #2443` — JINKY ZAPANTA ×2 — different phones (one N/A), email differs by one letter (`baloujie` vs `balouie`)
- [ ] `#2408, #2409` — DIANA / REY CADIZ — same phone `0212370926`, different names — could be household members, not necessarily a dupe

## Low confidence (same first name only, different phone — probably different people)

- [ ] `#2184, #2194` — JUN / JUN — different phone, no overlap other than first name. **Note:** `#2184` has email `junanthony.business@gmail.com` (the owner's own email) — check whether this is a real customer or a leftover dev/test entry before touching it.
- [ ] `#2213, #2249` — ROBERT / ROBERT — common first name only
- [ ] `#2243, #2392` — LEO / LEO — common first name only
- [ ] `#2251, #2410` — SAIA / SAIA — common first name only
- [ ] `#2296, #2339` — MARIA / MARIA — common first name only

## Related: duplicate-rego cars (separate flag, same report)

Not customer duplicates per se, but the same report flagged 6 cars sharing a rego —
worth checking alongside the above since a shared rego is the strongest duplicate
signal available (see `#2329/#2330` above, which hits both lists):

- [ ] Rego `FHP930` — legacy Car IDs `#42, #2217`
- [ ] Rego `GCZ193` — legacy Car IDs `#2114, #2172`
- [ ] Rego `MEY543` — legacy Car IDs `#2125, #2130`
- [ ] Rego `QNE330` — legacy Car IDs `#2169, #2251`
- [ ] Rego `KAM459` — legacy Car IDs `#2256, #2257`
- [ ] Rego `PEB332` — legacy Car IDs `#2334, #2335`
