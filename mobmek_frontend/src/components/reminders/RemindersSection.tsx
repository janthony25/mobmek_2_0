import { getCars } from '@/api/cars'
import { getReminderTemplates } from '@/api/reminderTemplates'
import { createReminder, deleteReminder, getReminders, updateReminder } from '@/api/reminders'
import { CrudSection } from '@/components/crud/CrudSection'
import { ReminderForm } from './ReminderForm'
import { useAsync } from '@/hooks/useAsync'
import { notifyBoardChanged } from '@/lib/board'
import { date, orDash } from '@/lib/format'
import type { SelectOption } from '@/components/crud/types'
import type { Reminder, UpdateReminderRequest } from '@/types'

interface RemindersSectionProps {
  customerId: string
  /** When set, the section is scoped to one car and new reminders attach to it. */
  lockedCarId?: string
  description?: string
  title?: string
}

/** yyyy-mm-dd for "today", to flag overdue reminders. */
function todayISO(): string {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

export function RemindersSection({ customerId, lockedCarId, description, title = 'Reminders' }: RemindersSectionProps) {
  const templates = useAsync(() => getReminderTemplates(), [])
  const cars = useAsync(() => getCars(customerId), [customerId])

  const carOptions: SelectOption[] = (cars.data ?? []).map((c) => ({
    value: c.id,
    label: `${orDash(c.carMakeName)} ${c.carModelName ?? ''} (${c.rego})`.trim(),
  }))

  const today = todayISO()

  return (
    <CrudSection<Reminder>
      resourceName="Reminder"
      title={title}
      variant="section"
      description={description}
      load={() => getReminders({ customerId, carId: lockedCarId })}
      getId={(r) => r.id}
      rowLabel={(r) => r.title}
      columns={[
        { header: 'Reminder', cell: (r) => r.title, className: 'font-medium text-slate-900' },
        {
          header: 'Due',
          cell: (r) => (
            <span className={!r.isDone && r.dueDate < today ? 'font-medium text-red-600' : ''}>
              {date(r.dueDate)}
            </span>
          ),
        },
        ...(lockedCarId ? [] : [{ header: 'Car', cell: (r: Reminder) => orDash(r.carLabel) }]),
        {
          header: 'Status',
          cell: (r) =>
            r.isDone ? (
              <span className="rounded bg-slate-100 px-2 py-0.5 text-xs text-slate-500">Done</span>
            ) : (
              <span className="rounded bg-amber-100 px-2 py-0.5 text-xs text-amber-700">Open</span>
            ),
        },
      ]}
      renderForm={(props) => (
        <ReminderForm
          initial={props.initial}
          templates={templates.data ?? []}
          carOptions={carOptions}
          lockedCarId={lockedCarId}
          onSubmit={props.onSubmit}
          onCancel={props.onCancel}
        />
      )}
      onCreate={(v) =>
        createReminder({ ...(v as UpdateReminderRequest), customerId }).then(() => undefined)
      }
      onUpdate={(id, v) => updateReminder(id, v as UpdateReminderRequest).then(() => undefined)}
      onDelete={deleteReminder}
      onChanged={notifyBoardChanged}
      emptyText="No reminders yet"
    />
  )
}
