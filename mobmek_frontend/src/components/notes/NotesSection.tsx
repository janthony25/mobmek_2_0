import { getCustomers } from '@/api/customers'
import { createNote, deleteNote, getNotes, updateNote } from '@/api/notes'
import { CrudSection } from '@/components/crud/CrudSection'
import { NoteForm } from './NoteForm'
import { toNoteRequest } from './noteRequest'
import { useAsync } from '@/hooks/useAsync'
import { notifyBoardChanged } from '@/lib/board'
import { date } from '@/lib/format'
import type { Note, NoteRequest } from '@/types'

interface NotesSectionProps {
  customerId: string
  description?: string
  title?: string
  collapsible?: boolean
}

/** Customer-scoped notes, same shape as RemindersSection — the global NotesPanel board (every
 * note, system-wide) doesn't let you see just one customer's notes from their own page. */
export function NotesSection({ customerId, description, title = 'Notes', collapsible }: NotesSectionProps) {
  const customers = useAsync(getCustomers, [])
  const customerOptions = (customers.data ?? []).map((c) => ({ value: c.id, label: `${c.firstName} ${c.lastName}` }))

  return (
    <CrudSection<Note>
      resourceName="Note"
      title={title}
      variant="section"
      collapsible={collapsible}
      description={description}
      load={() => getNotes(customerId)}
      getId={(n) => n.id}
      rowLabel={(n) => n.title}
      columns={[
        { header: 'Note', cell: (n) => (n.isPinned ? `📌 ${n.title}` : n.title), className: 'font-medium text-slate-900' },
        { header: 'Due', cell: (n) => (n.dueDate ? date(n.dueDate) : '—') },
        {
          header: 'Status',
          cell: (n) =>
            n.isDone ? (
              <span className="rounded bg-slate-100 px-2 py-0.5 text-xs text-slate-500">Done</span>
            ) : (
              <span className="rounded bg-amber-100 px-2 py-0.5 text-xs text-amber-700">Open</span>
            ),
        },
      ]}
      renderForm={(props) => (
        <NoteForm
          initial={props.initial}
          customerOptions={customerOptions}
          lockedCustomerId={customerId}
          onSubmit={(values: NoteRequest) => props.onSubmit(values as unknown as Record<string, unknown>)}
          onCancel={props.onCancel}
        />
      )}
      onCreate={(v) => createNote({ ...(v as unknown as NoteRequest), customerId }).then(() => undefined)}
      onUpdate={(id, v) => updateNote(id, v as unknown as NoteRequest).then(() => undefined)}
      onDelete={deleteNote}
      actionsAsDropdown
      extraAction={{
        label: () => 'Mark as done',
        hidden: (n) => n.isDone,
        onClick: (n) => updateNote(n.id, { ...toNoteRequest(n), isDone: true }).then(() => undefined),
      }}
      onChanged={notifyBoardChanged}
      emptyText="No notes yet"
    />
  )
}
