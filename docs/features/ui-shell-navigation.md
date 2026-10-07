# UI Shell & Navigation

**Last verified:** 2026-10-07 (read against `mobmek_frontend/src/components/layout/{Sidebar,AppLayout}.tsx`, `mobmek_frontend/src/components/notes/NotesPanel.tsx`; live-verified in-browser via a headless Chromium pass logged in as the bootstrap Admin, screenshotted at 1440px/820px/390px widths plus both drawer-open states — **note:** that headless pass used a fixed viewport and did not reproduce the real-mobile-Safari bug below, found afterward via an actual iPhone against production)

Covers the persistent app shell: the left sidebar nav, the right-hand notes/reminders board, and responsive (tablet/mobile) behavior for both. Also covers two cross-cutting mobile/touch bugs found in shared components used throughout the rest of the app (`DropdownMenu`/`Combobox`/`AsyncCombobox`, `CrudSection`'s header row), since they were reported and fixed alongside the shell work and there's no more specific home for them.

## Status summary

| Sub-capability | Status | Key file(s) |
|---|---|---|
| Sidebar nav (desktop) | Working | `components/layout/Sidebar.tsx` |
| Sidebar collapse/expand (desktop, icon-only rail) | Working (fixed 2026-10-07 — was leaking into mobile) | `Sidebar.tsx:146` (`collapsed` state, persisted to `localStorage` under `mobmek:sidebar-collapsed`) |
| Sidebar responsive drawer (tablet/mobile, <1024px) | Working | `Sidebar.tsx:162-180` (fixed + `translate-x` transform, `lg:static lg:translate-x-0` to pin in-flow on desktop) |
| Notes panel (desktop, always visible) | Working | `components/notes/NotesPanel.tsx` |
| Notes panel responsive drawer (tablet/mobile, <1024px) | Working | `NotesPanel.tsx:140-146` (same fixed/off-canvas pattern, right-anchored) |
| Top bar (hamburger + notes toggle, tablet/mobile only) | Working (fixed 2026-10-07) | `components/layout/AppLayout.tsx:22` (`h-dvh`, was `h-screen`) |
| Auto-close drawers on navigation | Working | `AppLayout.tsx:16-19` (`useEffect` on `location.pathname`) |
| Icon set | Working | `lucide-react` (outline icons), replacing the prior emoji icons |
| Dropdown/combobox dismissal on touch (app-wide) | Working (fixed 2026-10-07) | `components/ui/DropdownMenu.tsx`, `components/forms/{Combobox,AsyncCombobox}.tsx`, `pages/CustomerDetailPage.tsx` (`InvoiceDateFilter`) |
| List-page header layout on narrow screens (app-wide, via `CrudSection`) | Working (fixed 2026-10-07) | `components/crud/CrudSection.tsx:213-248` |

## Details

### Sidebar — Working
Light theme (`bg-white`, `border-slate-200`), rounded-pill active state (`bg-indigo-50 text-indigo-600`), lucide icons at `Sidebar.tsx:1-33`. Nav structure unchanged from before (`NAV_GROUPS`, permission-filtered via `useAuth().hasPermission`) — only the visual treatment and icon source changed, groups are still always-expanded (no accordion/collapse-per-group).

### Responsive breakpoint — single cutoff at `lg` (1024px)
Both the sidebar and notes panel treat "desktop" as `≥1024px` and "tablet or mobile" as everything below that — one breakpoint, not three separate tablet/mobile layouts. Below `lg`, both panels become fixed, off-canvas, `z`-stacked drawers (`z-40` panel, `z-30` backdrop) toggled by state lifted into `AppLayout`; at `lg`+ the `lg:static lg:translate-x-0` classes pin them back in-flow and the backdrop/top bar never render (`lg:hidden`).

### Top bar — Working, tablet/mobile only (viewport-height bug fixed 2026-10-07)
`AppLayout.tsx` renders a `h-14` bar (`lg:hidden`) with a hamburger (opens the sidebar drawer) and a notes icon button (opens the notes drawer); hidden on `/notes-reminders` since the full board page replaces the notes panel there. Both `mobileSidebarOpen`/`mobileNotesOpen` reset to closed on every route change (`AppLayout.tsx:16-19`) so navigating via a drawer link auto-dismisses it.

**Bug (reported by the user live on an iPhone against production):** the top bar didn't stay pinned while scrolling, and page drag/scroll felt "stuck." Root cause: the shell's outer container used `h-screen` (`100vh`). On mobile Safari/Chrome, `100vh` is sized to the *largest* possible viewport (address bar collapsed), which is taller than the actually-visible area whenever the address bar is showing — so the shell overflowed the real viewport and the whole document (not just `<main>`, which has its own `overflow-y-auto`) became a second, outer scroll container. The top bar, meant to stay pinned as a `shrink-0` sibling of that inner scroll area, scrolled away with everything else as part of that outer scroll. **Fixed** by switching to `h-dvh` (dynamic viewport height, tracks the actually-visible area as browser chrome shows/hides) at `AppLayout.tsx:22`. Not caught by the earlier headless-Chromium verification pass since headless browsers report a fixed viewport with no dynamic toolbar. Not independently re-verified live post-fix (no browser automation available this session) — type-checked and linted clean; visually re-check on a real phone.

### Notes panel — Working
Unchanged content/behavior (sticky notes + upcoming reminders, `NotesPanel.tsx:34-115`); only addition is the `mobileOpen`/`onCloseMobile` props driving the off-canvas transform and a close (`X`) button shown only below `lg`.

### Sidebar collapse leaking into the mobile/tablet drawer — Working (fixed 2026-10-07)
**Bug (reported by the user, live on an iPhone against production):** the mobile off-canvas drawer showed icons with no text labels at all — nav items, group headings (WORKSHOP/TEMPLATES/etc.), and the Profile/Sign out rows in the footer were all missing their text, leaving only icons in a drawer that's `w-64` and has plenty of room for labels. Root cause: `collapsed` (`Sidebar.tsx:133`) is meant to be a desktop-only "icon rail" preference — its toggle button is `hidden lg:flex`, unreachable below the `lg` (1024px) breakpoint — but every label in the nav body hid itself with a plain `{!collapsed && <span>...}` JS conditional, which removes the element at *any* viewport, not just desktop. So once `collapsed` was `true` in this browser's `localStorage` (most likely set via a wide/tablet-width visit or iOS Safari's "Request Desktop Website" mode, which both cross the `lg` breakpoint and reveal the otherwise-hidden toggle), the mobile drawer lost its labels too — even though the drawer never offers a way to set `collapsed` itself. The brand title two lines above it in the same file (`className={collapsed ? 'lg:hidden' : ''}`) already used the correct CSS-scoped approach, which is what gave this away as an inconsistency rather than intended behavior; there was also a dead `lg:block` class still sitting on the group-heading `<p>`, a leftover from an incomplete version of the same fix.

**Fixed** by switching every label/heading in the nav body (group headings, nav item labels, the user-name/Profile/Sign-out footer) from JS-conditional rendering to always rendering the content with a `collapsed ? 'lg:hidden' : ''` class, matching the brand title's pattern — so the icon-rail collapse can now only ever take effect at `lg:`+ (desktop), never in the mobile/tablet drawer. `navItemClass`'s `justify-center`/narrow padding (meant for centering a lone icon on the desktop rail) was similarly rescoped to `lg:justify-center lg:px-2` so it doesn't also apply to the now-visible label row on mobile. Not live-verified on a real device post-fix (no browser automation available this session) — type-checked and linted clean.

### Dropdown/combobox dismissal on touch — Working (fixed 2026-10-07)
**Bug:** the "Actions" menu on Invoices/Quotations/Documents rows, the `Combobox`/`AsyncCombobox` type-ahead pickers used throughout forms, and the inline date-filter dropdown on `CustomerDetailPage` could all get stuck open on mobile and not dismiss. Root cause: every one of these implements its own click-outside-to-close via a `document.addEventListener('mousedown', ...)` listener only. iOS Safari doesn't reliably dispatch a synthetic `mousedown`/`click` for a tap unless the tapped element is itself "clickable" (a link, button, input, or has its own click handler) — tapping a plain background `<div>` or text to dismiss a menu can fire no `mousedown` at all, so the menu just stays open until the user happens to tap something that is clickable.

**Fixed** by also listening for `touchstart` (which fires unconditionally regardless of the tapped element) alongside `mousedown` in all four call sites: `components/ui/DropdownMenu.tsx`, `components/forms/Combobox.tsx`, `components/forms/AsyncCombobox.tsx`, and the `InvoiceDateFilter` helper in `pages/CustomerDetailPage.tsx`. Diagnosed via code review (no live mobile repro available this session, browser automation extension wasn't connected) — type-checked and linted clean; not live-verified on a real device post-fix.

### List-page header layout on narrow screens — Working (fixed 2026-10-07)
**Bug:** on every page built on `CrudSection` (Customers, Job Center, and the rest of the CRUD list pages), the title/description block and the search+view-toggle+Add-button controls sat in one non-wrapping flex row (`CrudSection.tsx:213`, `flex items-end justify-between gap-4`). On a phone-width screen the fixed-width controls (a 192px search box plus the Cards/List toggle) left too little room for the title block, which has no `min-w-0`/wrap allowance, squeezing "Customers (1)" and its description paragraph into a narrow column and, per the user's screenshot, clipping the view-toggle off the right edge of the screen.

**Fixed** by making the header stack vertically below the `sm` breakpoint (`flex-col` → `sm:flex-row`) and letting the controls row wrap (`flex-wrap`) instead of overflowing, at `CrudSection.tsx:213,248`. Diagnosed from the user's screenshot + code review; type-checked and linted clean, not live-verified on a real device post-fix.

## Known gaps / follow-ups
- Nav groups (Workshop, Templates, Finance, etc.) are not individually collapsible/accordion — all expanded at once, matching prior behavior. Not attempted since it wasn't requested.
- No dedicated automated test coverage for the responsive behavior (no test suite exists in this frontend at all, per `mobmek_frontend/CLAUDE.md`); verified only via one live browser pass.
- The three mobile bugs above (viewport height, touch dismissal, header layout) were diagnosed and fixed via code review this session without a live mobile/tablet re-check — browser automation wasn't available. Worth a real-device pass (iPhone + an Android tablet, since the user mentioned tablets "in some pages" too) to confirm and to catch anything similar not yet reported.
