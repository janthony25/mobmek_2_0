import { useEffect, useState } from 'react'
import { getCarMakes } from '@/api/carMakes'
import { getCarModels } from '@/api/carModels'
import { Button } from '@/components/ui/Button'
import { Field, controlClass } from './controls'
import type { Car, CarMake, CarModel } from '@/types'

interface CarFormProps {
  initial: Car | null
  onSubmit: (values: Record<string, unknown>) => Promise<void>
  onCancel: () => void
}

/** Bespoke car form: selecting a make loads its models (cascade). */
export function CarForm({ initial, onSubmit, onCancel }: CarFormProps) {
  const [makes, setMakes] = useState<CarMake[]>([])
  const [models, setModels] = useState<CarModel[]>([])

  const [makeId, setMakeId] = useState(initial?.carMakeId ?? '')
  const [modelId, setModelId] = useState(initial?.carModelId ?? '')
  const [year, setYear] = useState(initial ? String(initial.year) : '')
  const [rego, setRego] = useState(initial?.rego ?? '')
  const [vin, setVin] = useState(initial?.vin ?? '')
  const [color, setColor] = useState(initial?.color ?? '')
  const [engineType, setEngineType] = useState(initial?.engineType ?? '')
  const [odometer, setOdometer] = useState(initial?.odometer != null ? String(initial.odometer) : '')

  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    getCarMakes().then(setMakes).catch(() => setMakes([]))
  }, [])

  // Load models whenever the selected make changes.
  useEffect(() => {
    if (!makeId) {
      setModels([])
      return
    }
    getCarModels(makeId).then(setModels).catch(() => setModels([]))
  }, [makeId])

  const handleMakeChange = (value: string) => {
    setMakeId(value)
    setModelId('') // model no longer valid under a different make
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!makeId || !modelId || !year.trim() || !rego.trim()) {
      setError('Make, model, year and rego are required.')
      return
    }
    setBusy(true)
    setError(null)
    try {
      await onSubmit({
        carMakeId: makeId,
        carModelId: modelId,
        year: Number(year),
        rego: rego.trim(),
        vin: vin.trim() || null,
        color: color.trim() || null,
        engineType: engineType.trim() || null,
        odometer: odometer.trim() === '' ? null : Number(odometer),
      })
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Field label="Make" required>
          <select value={makeId} onChange={(e) => handleMakeChange(e.target.value)} className={controlClass}>
            <option value="">Select…</option>
            {makes.map((m) => (
              <option key={m.id} value={m.id}>
                {m.name}
              </option>
            ))}
          </select>
        </Field>

        <Field label="Model" required>
          <select
            value={modelId}
            onChange={(e) => setModelId(e.target.value)}
            disabled={!makeId}
            className={controlClass}
          >
            <option value="">{makeId ? 'Select…' : 'Pick a make first'}</option>
            {models.map((m) => (
              <option key={m.id} value={m.id}>
                {m.name}
              </option>
            ))}
          </select>
        </Field>

        <Field label="Year" required>
          <input type="number" value={year} min={1900} max={2100} onChange={(e) => setYear(e.target.value)} className={controlClass} />
        </Field>
        <Field label="Rego" required>
          <input type="text" value={rego} onChange={(e) => setRego(e.target.value)} className={controlClass} />
        </Field>
        <Field label="VIN">
          <input type="text" value={vin ?? ''} onChange={(e) => setVin(e.target.value)} className={controlClass} />
        </Field>
        <Field label="Color">
          <input type="text" value={color ?? ''} onChange={(e) => setColor(e.target.value)} className={controlClass} />
        </Field>
        <Field label="Engine type">
          <input type="text" value={engineType ?? ''} onChange={(e) => setEngineType(e.target.value)} className={controlClass} />
        </Field>
        <Field label="Odometer">
          <input type="number" value={odometer} min={0} onChange={(e) => setOdometer(e.target.value)} className={controlClass} />
        </Field>
      </div>

      {error && <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>}

      <div className="flex justify-end gap-2 border-t border-slate-100 pt-4">
        <Button variant="secondary" onClick={onCancel} disabled={busy}>
          Cancel
        </Button>
        <Button type="submit" disabled={busy}>
          {busy ? 'Saving…' : 'Save'}
        </Button>
      </div>
    </form>
  )
}
