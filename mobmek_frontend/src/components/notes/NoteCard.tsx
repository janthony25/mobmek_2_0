import { Link } from 'react-router-dom'
import { date } from '@/lib/format'
import { noteCardClass } from './colors'
import type { Note, NoteRequest } from '@/types'

/** First 20 words of a note body; the rest is hidden behind a card click. */
function preview(text: string, maxWords = 20): string {
  const words = text.trim().split(/\s+/)
  return words.length <= maxWords ? text : `${words.slice(0, maxWords).join(' ')}…`
}

interface NoteCardProps {
  note: Note
  /** "yyyy-mm-dd" bounds shared by all cards so they agree on overdue/due-soon. */
  today: string
  soon: string
  onOpen: () => void
  onPatch: (patch: Partial<NoteRequest>) => void
  onDelete: () => void
}

/** Sticky-note card shared by the board panel and the Notes & Reminders page. */
export function NoteCard({ note, today, soon, onOpen, onPatch, onDelete }: NoteCardProps) {
  return (
    <div
      role="button"
      tabIndex={0}
      onClick={onOpen}
      onKeyDown={(e) => {
        if (e.key === 'Enter') onOpen()
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
        <CardAction onClick={() => onPatch({ isPinned: !note.isPinned })}>
          {note.isPinned ? 'Unpin' : 'Pin'}
        </CardAction>
        <CardAction onClick={() => onPatch({ isDone: !note.isDone })}>
          {note.isDone ? 'Reopen' : 'Done'}
        </CardAction>
        <CardAction onClick={onDelete} className="text-red-600">
          Delete
        </CardAction>
      </div>
    </div>
  )
}

function CardAction({
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

export function NoteDueBadge({
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
