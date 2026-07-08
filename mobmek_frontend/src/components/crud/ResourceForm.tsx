import { useState } from 'react'
import { Button } from '@/components/ui/Button'
import { Spinner } from '@/components/ui/Spinner'
import type { FieldSchema } from './types'

type FormState = Record<string, string | boolean>

interface ResourceFormProps {
  fields: FieldSchema[]
  /** Existing record to edit (read by field name), or null when creating. */
  initial?: Record<string, unknown> | null
  submitLabel?: string
  onSubmit: (values: Record<string, unknown>) => Promise<void>
  onCancel: () => void
}

function buildInitialState(fields: FieldSchema[], source?: Record<string, unknown> | null): FormState {
  const state: FormState = {}
  for (const field of fields) {
    if (field.type === 'checkbox') {
      const raw = source ? source[field.name] : field.defaultValue
      state[field.name] = Boolean(raw)
      continue
    }
    const raw = source ? source[field.name] : field.defaultValue
    state[field.name] = raw === null || raw === undefined ? '' : String(raw)
  }
  return state
}

function serialize(fields: FieldSchema[], state: FormState): Record<string, unknown> {
  const values: Record<string, unknown> = {}
  for (const field of fields) {
    if (field.type === 'checkbox') {
      values[field.name] = Boolean(state[field.name])
      continue
    }
    const raw = (state[field.name] as string).trim()
    if (field.type === 'number' || field.numeric) {
      values[field.name] = raw === '' ? null : Number(raw)
    } else {
      values[field.name] = raw === '' ? (field.required ? '' : null) : raw
    }
  }
  return values
}

export function ResourceForm({
  fields,
  initial,
  submitLabel = 'Save',
  onSubmit,
  onCancel,
}: ResourceFormProps) {
  const [state, setState] = useState<FormState>(() => buildInitialState(fields, initial))
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const setField = (name: string, value: string | boolean) =>
    setState((current) => ({ ...current, [name]: value }))

  const validate = (): boolean => {
    const next: Record<string, string> = {}
    for (const field of fields) {
      if (field.required && field.type !== 'checkbox') {
        const value = (state[field.name] as string).trim()
        if (value === '') next[field.name] = 'Required'
      }
    }
    setErrors(next)
    return Object.keys(next).length === 0
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!validate()) return
    setBusy(true)
    setFormError(null)
    try {
      await onSubmit(serialize(fields, state))
    } catch (err) {
      setFormError(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        {fields.map((field) => (
          <div
            key={field.name}
            className={field.type === 'textarea' ? 'sm:col-span-2' : undefined}
          >
            <FieldControl
              field={field}
              value={state[field.name]}
              error={errors[field.name]}
              onChange={(v) => setField(field.name, v)}
            />
          </div>
        ))}
      </div>

      {formError && (
        <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{formError}</p>
      )}

      <div className="flex justify-end gap-2 border-t border-slate-100 pt-4">
        <Button variant="secondary" onClick={onCancel} disabled={busy}>
          Cancel
        </Button>
        <Button type="submit" disabled={busy}>
          {busy ? (
            <>
              <Spinner className="h-3.5 w-3.5 text-white" /> Saving…
            </>
          ) : (
            submitLabel
          )}
        </Button>
      </div>
    </form>
  )
}

interface FieldControlProps {
  field: FieldSchema
  value: string | boolean
  error?: string
  onChange: (value: string | boolean) => void
}

const inputClass =
  'mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500'

function FieldControl({ field, value, error, onChange }: FieldControlProps) {
  if (field.type === 'checkbox') {
    return (
      <label className="mt-6 flex items-center gap-2 text-sm text-slate-700">
        <input
          type="checkbox"
          checked={Boolean(value)}
          onChange={(e) => onChange(e.target.checked)}
          className="h-4 w-4 rounded border-slate-300"
        />
        {field.label}
      </label>
    )
  }

  return (
    <label className="block text-sm font-medium text-slate-700">
      <span>
        {field.label}
        {field.required && <span className="text-red-500"> *</span>}
      </span>

      {field.type === 'textarea' && (
        <textarea
          value={value as string}
          placeholder={field.placeholder}
          onChange={(e) => onChange(e.target.value)}
          rows={3}
          className={inputClass}
        />
      )}

      {field.type === 'select' && (
        <select
          value={value as string}
          onChange={(e) => onChange(e.target.value)}
          className={inputClass}
        >
          <option value="">Select…</option>
          {field.options?.map((opt) => (
            <option key={opt.value} value={opt.value}>
              {opt.label}
            </option>
          ))}
        </select>
      )}

      {(field.type === 'text' || field.type === 'email' || field.type === 'number') && (
        <input
          type={field.type === 'number' ? 'number' : field.type === 'email' ? 'email' : 'text'}
          value={value as string}
          placeholder={field.placeholder}
          step={field.step}
          min={field.min}
          max={field.max}
          onChange={(e) => onChange(field.type === 'text' ? e.target.value.toUpperCase() : e.target.value)}
          className={inputClass}
        />
      )}

      {field.help && <span className="mt-1 block text-xs font-normal text-slate-400">{field.help}</span>}
      {error && <span className="mt-1 block text-xs font-normal text-red-500">{error}</span>}
    </label>
  )
}
