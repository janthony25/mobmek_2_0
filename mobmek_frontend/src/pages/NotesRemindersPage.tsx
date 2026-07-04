import { useEffect, useState } from 'react'
import { getCustomers } from '@/api/customers'
import { createNote, deleteNote, getNotes, updateNote } from '@/api/notes'
import { getReminders, updateReminder } from '@/api/reminders'
import { NoteCard } from '@/components/notes/NoteCard'
import { toNoteRequest } from '@/components/notes/noteRequest'
import { NoteForm } from '@/components/notes/NoteForm'
import { NoteViewModal } from '@/components/notes/NoteViewModal'
import { ReminderCard } from '@/components/reminders/ReminderCard'
import { ReminderDetailsModal } from '@/components/reminders/ReminderDetailsModal'
import { Button } from '@/components/ui/Button'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { Modal } from '@/components/ui/Modal'
import { PageHeader } from '@/components/ui/PageHeader'
import { StateMessage } from '@/components/ui/StateMessage'
import { useToast } from '@/components/ui/toast'
import { useAsync } from '@/hooks/useAsync'
import { notifyBoardChanged, onBoardChanged } from '@/lib/board'
import { isoInDays, todayISO } from '@/lib/dueDate'
import type { Note, NoteRequest, Reminder } from '@/types'

const cardGrid = 'grid gap-3 sm:grid-cols-2 xl:grid-cols-3'

/**
 * The full board: every sticky note (including ones done more than 24h ago,
 * which the side panel hides) and every reminder, done or not. Reached via the
 * « button on the side panel.
 */
export function NotesRemindersPage() {
  const toast = useToast()
  const notes = useAsync(getNotes, [])
  const reminders = useAsync(() => getReminders({ includeDone: true }), [])
  const customers = useAsync(getCustomers, [])

  const [editing, setEditing] = useState<Note | 'new' | null>(null)
  const [viewing, setViewing] = useState<Note | null>(null)
  const [deleting, setDeleting] = useState<Note | null>(null)
  const [viewingReminder, setViewingReminder] = useState<Reminder | null>(null)

  // Stay in sync with the side panel and detail pages via the board event.
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
  const openReminders = (reminders.data ?? []).filter((r) => !r.isDone)
  const doneReminders = (reminders.data ?? []).filter((r) => r.isDone)

  return (
    <div>
      <PageHeader
        title="Notes & Reminders"
        description="Every sticky note and reminder in one place — including notes done more than 24 hours ago, which the side board hides."
      />

      <section className="mb-10">
        <div className="mb-3 flex items-center justify-between">
          <h2 className="text-lg font-semibold text-slate-900">📌 Notes</h2>
          <Button onClick={() => setEditing('new')}>+ Add note</Button>
        </div>
        {notes.loading && !notes.data && <StateMessage title="Loading notes…" loading />}
        {notes.error && <StateMessage title="Could not load notes" description={notes.error.message} />}
        {notes.data && notes.data.length === 0 && (
          <StateMessage title="No notes yet" description="Add one and it shows up here and on the side board." />
        )}
        <div className={cardGrid}>
          {notes.data?.map((note) => (
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
      </section>

      <section>
        <h2 className="mb-3 text-lg font-semibold text-slate-900">⏰ Reminders</h2>
        {reminders.loading && !reminders.data && <StateMessage title="Loading reminders…" loading />}
        {reminders.error && (
          <StateMessage title="Could not load reminders" description={reminders.error.message} />
        )}
        {reminders.data && reminders.data.length === 0 && (
          <StateMessage title="No reminders yet" description="Add reminders from a customer or car page." />
        )}
        <div className={cardGrid}>
          {openReminders.map((r) => (
            <ReminderCard
              key={r.id}
              reminder={r}
              today={today}
              onOpen={() => setViewingReminder(r)}
              onComplete={() => completeReminder(r)}
            />
          ))}
        </div>
        {doneReminders.length > 0 && (
          <>
            <h3 className="mb-2 mt-6 text-sm font-semibold uppercase tracking-wider text-slate-500">Done</h3>
            <div className={cardGrid}>
              {doneReminders.map((r) => (
                <ReminderCard
                  key={r.id}
                  reminder={r}
                  today={today}
                  onOpen={() => setViewingReminder(r)}
                  onComplete={() => completeReminder(r)}
                />
              ))}
            </div>
          </>
        )}
      </section>

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
    </div>
  )
}
