import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { NoteDueBadge } from './NoteCard'
import type { Note } from '@/types'

interface NoteViewModalProps {
  /** The note being viewed; null keeps the modal closed. */
  note: Note | null
  /** "yyyy-mm-dd" bounds for the due badge. */
  today: string
  soon: string
  onClose: () => void
  onEdit: (note: Note) => void
  onDelete: (note: Note) => void
}

/** Read-only note details with Edit/Delete, shared by the panel and the full page. */
export function NoteViewModal({ note, today, soon, onClose, onEdit, onDelete }: NoteViewModalProps) {
  return (
    <Modal open={note !== null} title="Note" onClose={onClose}>
      {note && (
        <div className="space-y-3">
          <div>
            <h3 className={`text-base font-semibold text-slate-900 ${note.isDone ? 'line-through' : ''}`}>
              {note.isPinned && '📌 '}
              {note.title}
            </h3>
            {note.dueDate && (
              <NoteDueBadge dueDate={note.dueDate} isDone={note.isDone} today={today} soon={soon} />
            )}
          </div>
          {note.body && <p className="whitespace-pre-wrap text-sm text-slate-700">{note.body}</p>}
          {note.customerId && (
            <Link
              to={`/customers/${note.customerId}`}
              onClick={onClose}
              className="inline-block text-sm font-medium text-slate-600 hover:underline"
            >
              {note.customerName}
            </Link>
          )}
          <div className="flex flex-wrap gap-2 text-xs text-slate-500">
            {note.isPinned && <span className="rounded bg-slate-100 px-2 py-0.5">Pinned</span>}
            <span className="rounded bg-slate-100 px-2 py-0.5">{note.isDone ? 'Done' : 'Open'}</span>
          </div>
          <div className="flex justify-end gap-2 border-t border-slate-100 pt-4">
            <Button variant="danger" onClick={() => onDelete(note)}>
              Delete
            </Button>
            <Button onClick={() => onEdit(note)}>Edit</Button>
          </div>
        </div>
      )}
    </Modal>
  )
}
