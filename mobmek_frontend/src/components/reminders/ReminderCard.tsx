import { Link } from 'react-router-dom'
import { date } from '@/lib/format'
import type { Reminder } from '@/types'

interface ReminderCardProps {
  reminder: Reminder
  /** "yyyy-mm-dd" for today, so all cards agree on what's overdue. */
  today: string
  onOpen: () => void
  /** Marks the reminder done; the ✓ is hidden once it already is. */
  onComplete: () => void
}

/** Reminder card shared by the board panel and the Notes & Reminders page. */
export function ReminderCard({ reminder, today, onOpen, onComplete }: ReminderCardProps) {
  const overdue = !reminder.isDone && reminder.dueDate < today
  return (
    <div
      role="button"
      tabIndex={0}
      onClick={onOpen}
      onKeyDown={(e) => {
        if (e.key === 'Enter') onOpen()
      }}
      className={`cursor-pointer rounded-lg border border-slate-200 bg-white p-3 shadow-sm transition hover:shadow-md ${
        reminder.isDone ? 'opacity-60' : ''
      }`}
    >
      <div className="flex items-start justify-between gap-2">
        <p className={`text-sm font-medium text-slate-800 ${reminder.isDone ? 'line-through' : ''}`}>
          {reminder.title}
        </p>
        {!reminder.isDone && (
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation() // don't open the details modal
              onComplete()
            }}
            title="Mark done"
            className="shrink-0 rounded px-1.5 py-0.5 text-xs text-slate-400 hover:bg-slate-100 hover:text-slate-700"
          >
            ✓
          </button>
        )}
      </div>
      <p className={`text-xs ${overdue ? 'font-semibold text-red-600' : 'text-slate-500'}`}>
        {reminder.isDone ? 'Done · ' : overdue ? 'Overdue · ' : ''}
        {date(reminder.dueDate)}
      </p>
      <Link
        to={`/customers/${reminder.customerId}`}
        onClick={(e) => e.stopPropagation()}
        className="text-xs text-slate-500 hover:underline"
      >
        {reminder.customerName}
        {reminder.carLabel ? ` · ${reminder.carLabel}` : ''}
      </Link>
    </div>
  )
}
