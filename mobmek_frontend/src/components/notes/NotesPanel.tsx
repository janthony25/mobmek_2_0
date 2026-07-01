import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getCustomers } from '@/api/customers'
import { createNote, deleteNote, getNotes, updateNote } from '@/api/notes'
import { getReminders, updateReminder } from '@/api/reminders'
import { Button } from '@/components/ui/Button'
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
    dueDate: note.dueDate,
    color: note.color,
    isPinned: note.isPinned,
    isDone: note.isDone,
    customerId: note.customerId,
  }
}

function toISO(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

function todayISO(): string {
  return toISO(new Date())
}

function isoInDays(days: number): string {
  const d = new Date()
  d.setDate(d.getDate() + days)
  return toISO(d)
}

/** First 20 words of a note body; the rest is hidden behind a card click. */
function preview(text: string, maxWords = 20): string {
  const words = text.trim().split(/\s+/)
  return words.length <= maxWords ? text : `${words.slice(0, maxWords).join(' ')}…`
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
  const [viewing, setViewing] = useState<Note | null>(null)
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
  const soon = isoInDays(7)
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
            role="button"
            tabIndex={0}
            onClick={() => setViewing(note)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') setViewing(note)
            }}
            className={`cursor-pointer rounded-lg border p-3 text-left shadow-sm transition hover:shadow-md ${noteCardClass(
              note.color,
            )} ${note.isDone ? 'opacity-60' : ''}`}
          >
            <p className={`text-sm font-semibold text-slate-800 ${note.isDone ? 'line-through' : ''}`}>
              {note.isPinned && <span title="Pinned">📌 </span>}
              {note.title}
            </p>
            {note.dueDate && <NoteDueBadge dueDate={note.dueDate} isDone={note.isDone} today={today} soon={soon} />}
            {note.body && (
              <p className="mt-1 whitespace-pre-wrap text-xs text-slate-600">{preview(note.body)}</p>
            )}
            {note.customerId && (
              <Link
                to={`/customers/${note.customerId}`}
                onClick={(e) => e.stopPropagation()}
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
          ⏰ Reminders
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

      <Modal open={viewing !== null} title="Note" onClose={() => setViewing(null)}>
        {viewing && (
          <div className="space-y-3">
            <div>
              <h3 className={`text-base font-semibold text-slate-900 ${viewing.isDone ? 'line-through' : ''}`}>
                {viewing.isPinned && '📌 '}
                {viewing.title}
              </h3>
              {viewing.dueDate && (
                <NoteDueBadge dueDate={viewing.dueDate} isDone={viewing.isDone} today={today} soon={soon} />
              )}
            </div>
            {viewing.body && (
              <p className="whitespace-pre-wrap text-sm text-slate-700">{viewing.body}</p>
            )}
            {viewing.customerId && (
              <Link
                to={`/customers/${viewing.customerId}`}
                onClick={() => setViewing(null)}
                className="inline-block text-sm font-medium text-slate-600 hover:underline"
              >
                {viewing.customerName}
              </Link>
            )}
            <div className="flex flex-wrap gap-2 text-xs text-slate-500">
              {viewing.isPinned && <span className="rounded bg-slate-100 px-2 py-0.5">Pinned</span>}
              <span className="rounded bg-slate-100 px-2 py-0.5">{viewing.isDone ? 'Done' : 'Open'}</span>
            </div>
            <div className="flex justify-end gap-2 border-t border-slate-100 pt-4">
              <Button
                variant="danger"
                onClick={() => {
                  const note = viewing
                  setViewing(null)
                  setDeleting(note)
                }}
              >
                Delete
              </Button>
              <Button
                onClick={() => {
                  const note = viewing
                  setViewing(null)
                  setEditing(note)
                }}
              >
                Edit
              </Button>
            </div>
          </div>
        )}
      </Modal>

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
      onClick={(e) => {
        e.stopPropagation() // don't open the card's view modal
        onClick()
      }}
      className={`rounded px-1.5 py-0.5 font-medium text-slate-600 hover:bg-white/60 ${className}`}
    >
      {children}
    </button>
  )
}

function NoteDueBadge({
  dueDate,
  isDone,
  today,
  soon,
}: {
  dueDate: string
  isDone: boolean
  today: string
  soon: string
}) {
  const overdue = !isDone && dueDate < today
  const dueSoon = !isDone && !overdue && dueDate <= soon
  const tone = overdue ? 'text-red-600' : dueSoon ? 'text-amber-700' : 'text-slate-500'
  const label = overdue ? 'Overdue · ' : dueSoon ? 'Due soon · ' : 'Due '
  return (
    <p className={`mt-1 text-xs font-medium ${tone}`}>
      {label}
      {date(dueDate)}
    </p>
  )
}
