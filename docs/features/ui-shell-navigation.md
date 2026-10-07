# UI Shell & Navigation

**Last verified:** 2026-10-07 (read against `mobmek_frontend/src/components/layout/{Sidebar,AppLayout}.tsx`, `mobmek_frontend/src/components/notes/NotesPanel.tsx`; live-verified in-browser via a headless Chromium pass logged in as the bootstrap Admin, screenshotted at 1440px/820px/390px widths plus both drawer-open states)

Covers the persistent app shell: the left sidebar nav, the right-hand notes/reminders board, and responsive (tablet/mobile) behavior for both.

## Status summary

| Sub-capability | Status | Key file(s) |
|---|---|---|
| Sidebar nav (desktop) | Working | `components/layout/Sidebar.tsx` |
| Sidebar collapse/expand (desktop, icon-only rail) | Working | `Sidebar.tsx:146` (`collapsed` state, persisted to `localStorage` under `mobmek:sidebar-collapsed`) |
| Sidebar responsive drawer (tablet/mobile, <1024px) | Working | `Sidebar.tsx:162-180` (fixed + `translate-x` transform, `lg:static lg:translate-x-0` to pin in-flow on desktop) |
| Notes panel (desktop, always visible) | Working | `components/notes/NotesPanel.tsx` |
| Notes panel responsive drawer (tablet/mobile, <1024px) | Working | `NotesPanel.tsx:140-146` (same fixed/off-canvas pattern, right-anchored) |
| Top bar (hamburger + notes toggle, tablet/mobile only) | Working | `components/layout/AppLayout.tsx:25-47` (`lg:hidden`) |
| Auto-close drawers on navigation | Working | `AppLayout.tsx:16-19` (`useEffect` on `location.pathname`) |
| Icon set | Working | `lucide-react` (outline icons), replacing the prior emoji icons |

## Details

### Sidebar — Working
Light theme (`bg-white`, `border-slate-200`), rounded-pill active state (`bg-indigo-50 text-indigo-600`), lucide icons at `Sidebar.tsx:1-33`. Nav structure unchanged from before (`NAV_GROUPS`, permission-filtered via `useAuth().hasPermission`) — only the visual treatment and icon source changed, groups are still always-expanded (no accordion/collapse-per-group).

### Responsive breakpoint — single cutoff at `lg` (1024px)
Both the sidebar and notes panel treat "desktop" as `≥1024px` and "tablet or mobile" as everything below that — one breakpoint, not three separate tablet/mobile layouts. Below `lg`, both panels become fixed, off-canvas, `z`-stacked drawers (`z-40` panel, `z-30` backdrop) toggled by state lifted into `AppLayout`; at `lg`+ the `lg:static lg:translate-x-0` classes pin them back in-flow and the backdrop/top bar never render (`lg:hidden`).

### Top bar — Working, tablet/mobile only
`AppLayout.tsx` renders a `h-14` bar (`lg:hidden`) with a hamburger (opens the sidebar drawer) and a notes icon button (opens the notes drawer); hidden on `/notes-reminders` since the full board page replaces the notes panel there. Both `mobileSidebarOpen`/`mobileNotesOpen` reset to closed on every route change (`AppLayout.tsx:16-19`) so navigating via a drawer link auto-dismisses it.

### Notes panel — Working
Unchanged content/behavior (sticky notes + upcoming reminders, `NotesPanel.tsx:34-115`); only addition is the `mobileOpen`/`onCloseMobile` props driving the off-canvas transform and a close (`X`) button shown only below `lg`.

## Known gaps / follow-ups
- Nav groups (Workshop, Templates, Finance, etc.) are not individually collapsible/accordion — all expanded at once, matching prior behavior. Not attempted since it wasn't requested.
- No dedicated automated test coverage for the responsive behavior (no test suite exists in this frontend at all, per `mobmek_frontend/CLAUDE.md`); verified only via one live browser pass.
