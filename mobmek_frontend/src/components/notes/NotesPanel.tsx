import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getCustomers } from '@/api/customers'
import { createNote, deleteNote, getNotes, updateNote } from '@/api/notes'
import { getReminders, updateReminder } from '@/api/reminders'
import { Modal } from '@/components/ui/Modal'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { useToast } from '@/components/ui/toast'
import { useAsync } from '@/hooks/useAsync'
import { onBoardChanged } from '@/lib/board'
import { date } from '@/lib/format'
import { NoteForm } from './NoteForm'
import { noteCardClass } from './colors'
import type { Note, NoteRequest, Reminder } from '@/types'

function toRequest(note: Note): NoteRequest {
  return {
    title: note.title,
    body: note.body,
    color: note.color,
    isPinned: note.isPinned,
    isDone: note.isDone,
    customerId: note.customerId,
  }
}

function todayISO(): string {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

/**
 * Fixed right-hand board, mounted once by AppLayout so it stays visible on every
 * page. Not collapsible. Shows sticky notes plus an at-a-glance list of the next
 * reminders coming due across all customers.
 */
export function NotesPanel() {
  const toast = useToast()
  const notes = useAsync(getNotes, [])
  const reminders = useAsync(() => getReminders({ includeDone: false }), [])
  const customers = useAsync(getCustomers, [])

  const [editing, setEditing] = useState<Note | 'new' | null>(null)
  const [deleting, setDeleting] = useState<Note | null>(null)

  // Reload reminders when a detail page mutates them.
  const reloadReminders = reminders.reload
  useEffect(() => onBoardChanged(reloadReminders), [reloadReminders])

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
    notes.reload()
  }

  const patchNote = async (note: Note, patch: Partial<NoteRequest>) => {
    try {
      await updateNote(note.id, { ...toRequest(note), ...patch })
      notes.reload()
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
    notes.reload()
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
      reminders.reload()
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Could not update reminder')
    }
  }

  const today = todayISO()
  const upcoming = reminders.data ?? []

  return (
    <aside className="flex h-full w-80 shrink-0 flex-col overflow-y-auto border-l border-slate-200 bg-slate-100">
      {/* Notes */}
      <div className="flex items-center justify-between px-4 pb-2 pt-4">
        <h2 className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wider text-slate-500">
          📌 Notes
        </h2>
        <button
          type="button"
          onClick={() => setEditing('new')}
          className="rounded-md bg-slate-900 px-2 py-1 text-xs font-medium text-white hover:bg-slate-700"
        >
          + Add
        </button>
      </div>

      <div className="space-y-2 px-4 pb-4">
        {notes.loading && <p className="text-xs text-slate-400">Loading…</p>}
        {notes.error && <p className="text-xs text-red-500">{notes.error.message}</p>}
        {notes.data && notes.data.length === 0 && (
          <p className="text-xs text-slate-400">No notes yet. Add one to pin it here.</p>
        )}
        {notes.data?.map((note) => (
          <div
            key={note.id}
            className={`rounded-lg border p-3 shadow-sm ${noteCardClass(note.color)} ${
              note.isDone ? 'opacity-60' : ''
            }`}
          >
            <div className="flex items-start justify-between gap-2">
              <p className={`text-sm font-semibold text-slate-800 ${note.isDone ? 'line-through' : ''}`}>
                {note.isPinned && <span title="Pinned">📌 </span>}
                {note.title}
              </p>
            </div>
            {note.body && (
              <p className="mt-1 whitespace-pre-wrap text-xs text-slate-600">{note.body}</p>
            )}
            {note.customerId && (
              <Link
                to={`/customers/${note.customerId}`}
                className="mt-1 inline-block text-xs font-medium text-slate-500 hover:underline"
              >
                {note.customerName}
              </Link>
            )}
            <div className="mt-2 flex gap-1 text-xs">
              <PanelAction onClick={() => patchNote(note, { isPinned: !note.isPinned })}>
                {note.isPinned ? 'Unpin' : 'Pin'}
              </PanelAction>
              <PanelAction onClick={() => patchNote(note, { isDone: !note.isDone })}>
                {note.isDone ? 'Reopen' : 'Done'}
              </PanelAction>
              <PanelAction onClick={() => setEditing(note)}>Edit</PanelAction>
              <PanelAction onClick={() => setDeleting(note)} className="text-red-600">
                Delete
              </PanelAction>
            </div>
          </div>
        ))}
      </div>

      {/* Upcoming reminders */}
      <div className="mt-auto border-t border-slate-200 px-4 pb-4 pt-4">
        <h2 className="mb-2 flex items-center gap-2 text-sm font-semibold uppercase tracking-wider text-slate-500">
          ⏰ Due soon
        </h2>
        {reminders.loading && <p className="text-xs text-slate-400">Loading…</p>}
        {reminders.data && upcoming.length === 0 && (
          <p className="text-xs text-slate-400">Nothing outstanding. Add reminders from a customer or car.</p>
        )}
        <div className="space-y-2">
          {upcoming.map((r) => {
            const overdue = r.dueDate < today
            return (
              <div key={r.id} className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
                <div className="flex items-start justify-between gap-2">
                  <p className="text-sm font-medium text-slate-800">{r.title}</p>
                  <button
                    type="button"
                    onClick={() => completeReminder(r)}
                    title="Mark done"
                    className="shrink-0 rounded px-1.5 py-0.5 text-xs text-slate-400 hover:bg-slate-100 hover:text-slate-700"
                  >
                    ✓
                  </button>
                </div>
                <p className={`text-xs ${overdue ? 'font-semibold text-red-600' : 'text-slate-500'}`}>
                  {overdue ? 'Overdue · ' : ''}
                  {date(r.dueDate)}
                </p>
                <Link
                  to={`/customers/${r.customerId}`}
                  className="text-xs text-slate-500 hover:underline"
                >
                  {r.customerName}
                  {r.carLabel ? ` · ${r.carLabel}` : ''}
                </Link>
              </div>
            )
          })}
        </div>
      </div>

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
    </aside>
  )
}

function PanelAction({
  children,
  onClick,
  className = '',
}: {
  children: React.ReactNode
  onClick: () => void
  className?: string
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`rounded px-1.5 py-0.5 font-medium text-slate-600 hover:bg-white/60 ${className}`}
    >
      {children}
    </button>
  )
}
