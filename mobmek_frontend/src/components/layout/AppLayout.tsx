import { useEffect, useState } from 'react'
import { Menu, StickyNote } from 'lucide-react'
import { Outlet, useLocation } from 'react-router-dom'
import { Sidebar } from './Sidebar'
import { NotesPanel } from '@/components/notes/NotesPanel'

export function AppLayout() {
  const location = useLocation()
  // The full Notes & Reminders page replaces the board panel, so hide it there.
  const onBoardPage = location.pathname === '/notes-reminders'

  const [mobileSidebarOpen, setMobileSidebarOpen] = useState(false)
  const [mobileNotesOpen, setMobileNotesOpen] = useState(false)

  // Close off-canvas drawers whenever the route changes.
  useEffect(() => {
    setMobileSidebarOpen(false)
    setMobileNotesOpen(false)
  }, [location.pathname])

  return (
    <div className="flex h-screen bg-slate-50 text-slate-900">
      <Sidebar mobileOpen={mobileSidebarOpen} onCloseMobile={() => setMobileSidebarOpen(false)} />
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-14 shrink-0 items-center justify-between border-b border-slate-200 bg-white px-4 lg:hidden">
          <button
            type="button"
            onClick={() => setMobileSidebarOpen(true)}
            aria-label="Open menu"
            className="flex h-9 w-9 items-center justify-center rounded-lg text-slate-500 hover:bg-slate-50 hover:text-slate-900"
          >
            <Menu className="h-5 w-5" strokeWidth={1.75} />
          </button>
          <span className="text-sm font-semibold tracking-tight text-slate-900">Mobmek</span>
          {onBoardPage ? (
            <span className="h-9 w-9" aria-hidden />
          ) : (
            <button
              type="button"
              onClick={() => setMobileNotesOpen(true)}
              aria-label="Open notes"
              className="flex h-9 w-9 items-center justify-center rounded-lg text-slate-500 hover:bg-slate-50 hover:text-slate-900"
            >
              <StickyNote className="h-5 w-5" strokeWidth={1.75} />
            </button>
          )}
        </header>
        <main className="flex-1 overflow-y-auto">
          <div className="mx-auto max-w-6xl px-8 py-8">
            <Outlet />
          </div>
        </main>
      </div>
      {!onBoardPage && (
        <NotesPanel mobileOpen={mobileNotesOpen} onCloseMobile={() => setMobileNotesOpen(false)} />
      )}
    </div>
  )
}
