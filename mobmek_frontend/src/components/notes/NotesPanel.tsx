import { useEffect, useState } from 'react'
import { X } from 'lucide-react'
import { Link } from 'react-router-dom'
import { getCustomers } from '@/api/customers'
import { createNote, deleteNote, getNotes, updateNote } from '@/api/notes'
import { getReminders, updateReminder } from '@/api/reminders'
import { ReminderCard } from '@/components/reminders/ReminderCard'
import { ReminderDetailsModal } from '@/components/reminders/ReminderDetailsModal'
import { Modal } from '@/components/ui/Modal'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { useToast } from '@/components/ui/toast'
import { useAsync } from '@/hooks/useAsync'
import { notifyBoardChanged, onBoardChanged } from '@/lib/board'
import { isoInDays, todayISO } from '@/lib/dueDate'
import { NoteCard } from './NoteCard'
import { toNoteRequest } from './noteRequest'
import { NoteForm } from './NoteForm'
import { NoteViewModal } from './NoteViewModal'
import type { Note, NoteRequest, Reminder } from '@/types'

const DAY_MS = 24 * 60 * 60 * 1000

/** Done notes linger on the board for 24h, then only show on the full page. */
function visibleOnBoard(note: Note): boolean {
  if (!note.isDone || !note.doneAtUtc) return true
  return Date.now() - new Date(note.doneAtUtc).getTime() < DAY_MS
}

interface NotesPanelProps {
  /** Whether the off-canvas drawer is open on screens below `lg`. At `lg` and up the panel is always visible in-flow. */
  mobileOpen: boolean
  onCloseMobile: () => void
}

/**
 * Right-hand board, mounted once by AppLayout so it stays visible on every
 * page. Always visible in-flow at `lg` and up; below `lg` it's an off-canvas
 * drawer toggled from the top bar. Shows sticky notes plus an at-a-glance list
 * of the next reminders coming due across all customers; the « button opens
 * the full Notes & Reminders page (which also shows notes done more than 24h ago).
 */
export function NotesPanel({ mobileOpen, onCloseMobile }: NotesPanelProps) {
  const toast = useToast()
  const notes = useAsync(getNotes, [])
  const reminders = useAsync(() => getReminders({ includeDone: false }), [])
  const customers = useAsync(getCustomers, [])

  const [editing, setEditing] = useState<Note | 'new' | null>(null)
  const [viewing, setViewing] = useState<Note | null>(null)
  const [deleting, setDeleting] = useState<Note | null>(null)
  const [viewingReminder, setViewingReminder] = useState<Reminder | null>(null)

  // Reload whenever this panel, a detail page or the full board page mutates
  // notes/reminders — everyone announces via the same board event.
  const reloadNotes = notes.reload
  const reloadReminders = reminders.reload
  useEffect(
    () =>
      onBoardChanged(() => {
        reloadNotes()
        reloadReminders()
      }),
    [reloadNotes, reloadReminders],
  )

  const customerOptions = (customers.data ?? []).map((c) => ({
    value: c.id,
    label: `${c.firstName} ${c.lastName}`,
  }))

  const handleSave = async (values: NoteRequest) => {
    if (editing === 'new') {
      await createNote(values)
      toast.success('Note added')
    } else if (editing) {
      await updateNote(editing.id, values)
      toast.success('Note updated')
    }
    setEditing(null)
    notifyBoardChanged()
  }

  const patchNote = async (note: Note, patch: Partial<NoteRequest>) => {
    try {
      await updateNote(note.id, { ...toNoteRequest(note), ...patch })
      notifyBoardChanged()
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Could not update note')
    }
  }

  const handleDelete = async () => {
    if (!deleting) return
    try {
      await deleteNote(deleting.id)
      toast.success('Note deleted')
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Could not delete note')
    }
    setDeleting(null)
    notifyBoardChanged()
  }

  const completeReminder = async (r: Reminder) => {
    try {
      await updateReminder(r.id, {
        carId: r.carId,
        reminderTemplateId: r.reminderTemplateId,
        title: r.title,
        dueDate: r.dueDate,
        isDone: true,
        notes: r.notes,
      })
      notifyBoardChanged()
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Could not update reminder')
    }
  }

  const today = todayISO()
  const soon = isoInDays(7)
  const boardNotes = (notes.data ?? []).filter(visibleOnBoard)
  const upcoming = reminders.data ?? []

  return (
    <>
      {mobileOpen && (
        <div
          className="fixed inset-0 z-30 bg-slate-900/40 lg:hidden"
          onClick={onCloseMobile}
          aria-hidden
        />
      )}
      <aside
        className={[
          'fixed inset-y-0 right-0 z-40 flex h-full w-80 shrink-0 flex-col overflow-y-auto border-l border-slate-200 bg-slate-100 transition-transform duration-200',
          'lg:static lg:translate-x-0',
          mobileOpen ? 'translate-x-0' : 'translate-x-full',
        ].join(' ')}
      >
      {/* Notes */}
      <div className="flex items-center justify-between px-4 pb-2 pt-4">
        <div className="flex items-center gap-2">
          <Link
            to="/notes-reminders"
            title="Open the full Notes & Reminders page"
            className="rounded-md border border-slate-300 px-2 py-1 text-xs font-medium text-slate-600 hover:bg-white"
          >
            «
          </Link>
          <h2 className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wider text-slate-500">
            📌 Notes
          </h2>
        </div>
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => setEditing('new')}
            className="rounded-md bg-slate-900 px-2 py-1 text-xs font-medium text-white hover:bg-slate-700"
          >
            + Add
          </button>
          <button
            type="button"
            onClick={onCloseMobile}
            aria-label="Close notes panel"
            className="flex h-7 w-7 items-center justify-center rounded-md text-slate-400 hover:bg-white hover:text-slate-600 lg:hidden"
          >
            <X className="h-4 w-4" strokeWidth={1.75} />
          </button>
        </div>
      </div>

      <div className="space-y-2 px-4 pb-4">
        {notes.loading && <p className="text-xs text-slate-400">Loading…</p>}
        {notes.error && <p className="text-xs text-red-500">{notes.error.message}</p>}
        {notes.data && boardNotes.length === 0 && (
          <p className="text-xs text-slate-400">No notes yet. Add one to pin it here.</p>
        )}
        {boardNotes.map((note) => (
          <NoteCard
            key={note.id}
            note={note}
            today={today}
            soon={soon}
            onOpen={() => setViewing(note)}
            onPatch={(patch) => patchNote(note, patch)}
            onDelete={() => setDeleting(note)}
          />
        ))}
      </div>

      {/* Upcoming reminders */}
      <div className="mt-auto border-t border-slate-200 px-4 pb-4 pt-4">
        <h2 className="mb-2 flex items-center gap-2 text-sm font-semibold uppercase tracking-wider text-slate-500">
          ⏰ Reminders
        </h2>
        {reminders.loading && <p className="text-xs text-slate-400">Loading…</p>}
        {reminders.data && upcoming.length === 0 && (
          <p className="text-xs text-slate-400">Nothing outstanding. Add reminders from a customer or car.</p>
        )}
        <div className="space-y-2">
          {upcoming.map((r) => (
            <ReminderCard
              key={r.id}
              reminder={r}
              today={today}
              onOpen={() => setViewingReminder(r)}
              onComplete={() => completeReminder(r)}
            />
          ))}
        </div>
      </div>

      <NoteViewModal
        note={viewing}
        today={today}
        soon={soon}
        onClose={() => setViewing(null)}
        onEdit={(note) => {
          setViewing(null)
          setEditing(note)
        }}
        onDelete={(note) => {
          setViewing(null)
          setDeleting(note)
        }}
      />

      <Modal
        open={editing !== null}
        title={editing === 'new' ? 'Add note' : 'Edit note'}
        onClose={() => setEditing(null)}
      >
        {editing !== null && (
          <NoteForm
            initial={editing === 'new' ? null : editing}
            customerOptions={customerOptions}
            onSubmit={handleSave}
            onCancel={() => setEditing(null)}
          />
        )}
      </Modal>

      <ConfirmDialog
        open={deleting !== null}
        title="Delete note"
        message={deleting ? `Delete “${deleting.title}”? This cannot be undone.` : ''}
        onConfirm={handleDelete}
        onCancel={() => setDeleting(null)}
      />

      <ReminderDetailsModal
        reminder={viewingReminder}
        onClose={() => setViewingReminder(null)}
        onSaved={notifyBoardChanged}
      />
      </aside>
    </>
  )
}
