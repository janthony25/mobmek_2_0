import { useState } from 'react'
import { Link } from 'react-router-dom'
import { getCars } from '@/api/cars'
import { getReminderTemplates } from '@/api/reminderTemplates'
import { updateReminder } from '@/api/reminders'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { useToast } from '@/components/ui/toast'
import { useAsync } from '@/hooks/useAsync'
import { todayISO } from '@/lib/dueDate'
import { date, orDash } from '@/lib/format'
import { ReminderForm } from './ReminderForm'
import type { SelectOption } from '@/components/crud/types'
import type { Car, Reminder, ReminderTemplate, UpdateReminderRequest } from '@/types'

interface ReminderDetailsModalProps {
  /** The reminder being viewed; null keeps the modal closed. */
  reminder: Reminder | null
  onClose: () => void
  /** Called after a change is saved so the caller can refresh its list. */
  onSaved: () => void
}

/**
 * Full reminder details with an inline switch to the edit form, shared by the
 * board panel and the Notes & Reminders page. The edit form's lookups (presets +
 * the customer's cars) are only fetched once a reminder is opened.
 */
export function ReminderDetailsModal({ reminder, onClose, onSaved }: ReminderDetailsModalProps) {
  const toast = useToast()
  const [editing, setEditing] = useState(false)
  const customerId = reminder?.customerId ?? null

  const templates = useAsync<ReminderTemplate[]>(
    () => (customerId ? getReminderTemplates() : Promise.resolve([])),
    [customerId],
  )
  const cars = useAsync<Car[]>(
    () => (customerId ? getCars(customerId) : Promise.resolve([])),
    [customerId],
  )

  const carOptions: SelectOption[] = (cars.data ?? []).map((c) => ({
    value: c.id,
    label: `${orDash(c.carMakeName)} ${c.carModelName ?? ''} (${c.rego})`.trim(),
  }))

  const close = () => {
    setEditing(false)
    onClose()
  }

  const handleSave = async (values: Record<string, unknown>) => {
    if (!reminder) return
    await updateReminder(reminder.id, values as UpdateReminderRequest)
    toast.success('Reminder updated')
    close()
    onSaved()
  }

  const overdue = reminder !== null && !reminder.isDone && reminder.dueDate < todayISO()

  return (
    <Modal open={reminder !== null} title={editing ? 'Edit reminder' : 'Reminder'} onClose={close}>
      {reminder && !editing && (
        <div className="space-y-3">
          <div>
            <h3 className={`text-base font-semibold text-slate-900 ${reminder.isDone ? 'line-through' : ''}`}>
              {reminder.title}
            </h3>
            <p className={`mt-1 text-xs font-medium ${overdue ? 'text-red-600' : 'text-slate-500'}`}>
              {overdue ? 'Overdue · ' : 'Due '}
              {date(reminder.dueDate)}
            </p>
          </div>
          {reminder.notes && <p className="whitespace-pre-wrap text-sm text-slate-700">{reminder.notes}</p>}
          <Link
            to={`/customers/${reminder.customerId}`}
            onClick={close}
            className="inline-block text-sm font-medium text-slate-600 hover:underline"
          >
            {reminder.customerName}
            {reminder.carLabel ? ` · ${reminder.carLabel}` : ''}
          </Link>
          <div className="flex flex-wrap gap-2 text-xs text-slate-500">
            <span className="rounded bg-slate-100 px-2 py-0.5">{reminder.isDone ? 'Done' : 'Open'}</span>
            {reminder.reminderTemplateName && (
              <span className="rounded bg-slate-100 px-2 py-0.5">{reminder.reminderTemplateName}</span>
            )}
          </div>
          <div className="flex justify-end gap-2 border-t border-slate-100 pt-4">
            <Button onClick={() => setEditing(true)}>Edit</Button>
          </div>
        </div>
      )}
      {reminder && editing && (
        <ReminderForm
          initial={reminder}
          templates={templates.data ?? []}
          carOptions={carOptions}
          onSubmit={handleSave}
          onCancel={() => setEditing(false)}
        />
      )}
    </Modal>
  )
}
