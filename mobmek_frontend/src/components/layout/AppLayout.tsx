import { Outlet } from 'react-router-dom'
import { Sidebar } from './Sidebar'
import { NotesPanel } from '@/components/notes/NotesPanel'

export function AppLayout() {
  return (
    <div className="flex h-screen bg-slate-50 text-slate-900">
      <Sidebar />
      <main className="flex-1 overflow-y-auto">
        <div className="mx-auto max-w-6xl px-8 py-8">
          <Outlet />
        </div>
      </main>
      <NotesPanel />
    </div>
  )
}
