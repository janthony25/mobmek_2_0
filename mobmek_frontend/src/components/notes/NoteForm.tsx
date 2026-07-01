import { useState } from 'react'
import { Button } from '@/components/ui/Button'
import { Combobox } from '@/components/forms/Combobox'
import { Field, controlClass } from '@/components/forms/controls'
import { NOTE_COLORS } from './colors'
import type { SelectOption } from '@/components/crud/types'
import type { Note, NoteRequest } from '@/types'

interface NoteFormProps {
  initial: Note | null
  customerOptions: SelectOption[]
  onSubmit: (values: NoteRequest) => Promise<void>
  onCancel: () => void
}

export function NoteForm({ initial, customerOptions, onSubmit, onCancel }: NoteFormProps) {
  const [title, setTitle] = useState(initial?.title ?? '')
  const [body, setBody] = useState(initial?.body ?? '')
  const [color, setColor] = useState(initial?.color ?? NOTE_COLORS[0].key)
  const [isPinned, setIsPinned] = useState(initial?.isPinned ?? false)
  const [isDone, setIsDone] = useState(initial?.isDone ?? false)
  const [customerId, setCustomerId] = useState(initial?.customerId ?? '')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (title.trim() === '') {
      setError('Title is required.')
      return
    }
    setBusy(true)
    setError(null)
    try {
      await onSubmit({
        title: title.trim(),
        body: body.trim() === '' ? null : body.trim(),
        color,
        isPinned,
        isDone,
        customerId: customerId === '' ? null : customerId,
      })
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <Field label="Title" required>
        <input
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          placeholder="e.g. Order more oil filters"
          className={controlClass}
          autoFocus
        />
      </Field>

      <Field label="Details">
        <textarea
          value={body}
          onChange={(e) => setBody(e.target.value)}
          rows={3}
          className={controlClass}
        />
      </Field>

      <div>
        <span className="block text-sm font-medium text-slate-700">Colour</span>
        <div className="mt-1 flex gap-2">
          {NOTE_COLORS.map((c) => (
            <button
              key={c.key}
              type="button"
              onClick={() => setColor(c.key)}
              aria-label={c.label}
              title={c.label}
              className={`h-7 w-7 rounded-full border ${c.swatch} ${
                color === c.key ? 'ring-2 ring-slate-900 ring-offset-1' : 'border-slate-300'
              }`}
            />
          ))}
        </div>
      </div>

      <Field label="Linked customer (optional)">
        <Combobox
          options={customerOptions}
          value={customerId}
          onChange={setCustomerId}
          placeholder="Search customers…"
        />
      </Field>

      <div className="flex gap-6">
        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input
            type="checkbox"
            checked={isPinned}
            onChange={(e) => setIsPinned(e.target.checked)}
            className="h-4 w-4 rounded border-slate-300"
          />
          Pin to top
        </label>
        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input
            type="checkbox"
            checked={isDone}
            onChange={(e) => setIsDone(e.target.checked)}
            className="h-4 w-4 rounded border-slate-300"
          />
          Done
        </label>
      </div>

      {error && <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>}

      <div className="flex justify-end gap-2 border-t border-slate-100 pt-4">
        <Button variant="secondary" onClick={onCancel} disabled={busy}>
          Cancel
        </Button>
        <Button type="submit" disabled={busy}>
          {busy ? 'Saving…' : initial ? 'Save changes' : 'Add note'}
        </Button>
      </div>
    </form>
  )
}
