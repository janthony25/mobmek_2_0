import { useState } from 'react'
import { Button } from '@/components/ui/Button'
import { Field, controlClass } from '@/components/forms/controls'
import type { SelectOption } from '@/components/crud/types'
import type { Reminder, ReminderTemplate } from '@/types'

interface ReminderFormProps {
  initial: Reminder | null
  templates: ReminderTemplate[]
  /** The customer's cars to choose from. Ignored when `lockedCarId` is set. */
  carOptions: SelectOption[]
  /** When set, the reminder is tied to this car and the picker is hidden. */
  lockedCarId?: string
  /** Emits an UpdateReminderRequest-shaped object (customer added by the caller). */
  onSubmit: (values: Record<string, unknown>) => Promise<void>
  onCancel: () => void
}

/** Local yyyy-mm-dd (avoids the UTC off-by-one that toISOString can cause). */
function toISODate(d: Date): string {
  const month = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${d.getFullYear()}-${month}-${day}`
}

function addMonths(months: number): string {
  const d = new Date()
  d.setMonth(d.getMonth() + months)
  return toISODate(d)
}

export function ReminderForm({
  initial,
  templates,
  carOptions,
  lockedCarId,
  onSubmit,
  onCancel,
}: ReminderFormProps) {
  const [carId, setCarId] = useState(lockedCarId ?? initial?.carId ?? '')
  const [templateId, setTemplateId] = useState(initial?.reminderTemplateId ?? '')
  const [title, setTitle] = useState(initial?.title ?? '')
  const [dueDate, setDueDate] = useState(initial?.dueDate ?? toISODate(new Date()))
  const [isDone, setIsDone] = useState(initial?.isDone ?? false)
  const [notes, setNotes] = useState(initial?.notes ?? '')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  // Picking a preset fills in the title (if blank) and pre-computes the due date.
  const chooseTemplate = (id: string) => {
    setTemplateId(id)
    const template = templates.find((t) => t.id === id)
    if (!template) return
    if (title.trim() === '') setTitle(template.name)
    if (template.defaultIntervalMonths != null) setDueDate(addMonths(template.defaultIntervalMonths))
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (title.trim() === '') {
      setError('Title is required.')
      return
    }
    if (dueDate === '') {
      setError('Due date is required.')
      return
    }
    setBusy(true)
    setError(null)
    try {
      await onSubmit({
        carId: carId === '' ? null : carId,
        reminderTemplateId: templateId === '' ? null : templateId,
        title: title.trim(),
        dueDate,
        isDone,
        notes: notes.trim() === '' ? null : notes.trim(),
      })
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <Field label="Preset (optional)">
        <select value={templateId} onChange={(e) => chooseTemplate(e.target.value)} className={controlClass}>
          <option value="">None — type your own</option>
          {templates.map((t) => (
            <option key={t.id} value={t.id}>
              {t.name}
            </option>
          ))}
        </select>
      </Field>

      <Field label="Title" required>
        <input
          value={title}
          onChange={(e) => setTitle(e.target.value.toUpperCase())}
          placeholder="e.g. Next WOF"
          className={controlClass}
        />
      </Field>

      <Field label="Due date" required>
        <input
          type="date"
          value={dueDate}
          onChange={(e) => setDueDate(e.target.value)}
          className={controlClass}
        />
      </Field>

      {!lockedCarId && (
        <Field label="Car (optional)">
          <select value={carId} onChange={(e) => setCarId(e.target.value)} className={controlClass}>
            <option value="">No specific car</option>
            {carOptions.map((c) => (
              <option key={c.value} value={c.value}>
                {c.label}
              </option>
            ))}
          </select>
        </Field>
      )}

      <Field label="Notes">
        <textarea value={notes} onChange={(e) => setNotes(e.target.value)} rows={2} className={controlClass} />
      </Field>

      <label className="flex items-center gap-2 text-sm text-slate-700">
        <input
          type="checkbox"
          checked={isDone}
          onChange={(e) => setIsDone(e.target.checked)}
          className="h-4 w-4 rounded border-slate-300"
        />
        Done
      </label>

      {error && <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>}

      <div className="flex justify-end gap-2 border-t border-slate-100 pt-4">
        <Button variant="secondary" onClick={onCancel} disabled={busy}>
          Cancel
        </Button>
        <Button type="submit" disabled={busy}>
          {busy ? 'Saving…' : initial ? 'Save changes' : 'Add reminder'}
        </Button>
      </div>
    </form>
  )
}
