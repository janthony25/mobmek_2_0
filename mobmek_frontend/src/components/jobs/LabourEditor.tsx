import { Button } from '@/components/ui/Button'
import { Field, controlClass } from '@/components/forms/controls'
import { currency } from '@/lib/format'
import { computeLabour, type LabourDraft } from '@/lib/jobLineDrafts'

interface LabourEditorProps {
  labour: LabourDraft[]
  onAdd: () => void
  onUpdate: (key: string, patch: Partial<LabourDraft>) => void
  onRemove: (key: string) => void
}

/** Editable Labour cards, shared by the New Job form and the Job Details edit mode. */
export function LabourEditor({ labour, onAdd, onUpdate, onRemove }: LabourEditorProps) {
  return (
    <section className="rounded-lg border border-slate-200 bg-white p-5">
      <div className="mb-3 flex items-center justify-between">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">Labour</h2>
        <Button type="button" variant="secondary" size="sm" onClick={onAdd}>
          + Add labour
        </Button>
      </div>
      {labour.length === 0 ? (
        <p className="text-sm text-slate-500">No labour yet. Use “Add labour” to add a line.</p>
      ) : (
        <div className="space-y-3">
          {labour.map((l) => (
            <div key={l.key} className="rounded-md border border-slate-200 p-3">
              <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
                <Field label="Hours">
                  <input type="number" step="0.1" min={0} value={l.hours} onChange={(e) => onUpdate(l.key, { hours: e.target.value })} className={controlClass} />
                </Field>
                <Field label="Rate / hour">
                  <input type="number" step="0.01" min={0} value={l.ratePerHour} onChange={(e) => onUpdate(l.key, { ratePerHour: e.target.value })} className={controlClass} />
                </Field>
                <Field label="Fixed amount">
                  <input
                    type="number"
                    step="0.01"
                    min={0}
                    value={l.fixedAmount}
                    onChange={(e) => onUpdate(l.key, { fixedAmount: e.target.value })}
                    className={controlClass}
                    placeholder="Overrides hours × rate"
                  />
                </Field>
                <div className="flex items-end justify-between gap-2">
                  <div className="text-sm text-slate-600">
                    Total: <strong className="text-slate-900">{currency(computeLabour(l))}</strong>
                  </div>
                  <Button type="button" variant="ghost" size="sm" className="text-red-600" onClick={() => onRemove(l.key)}>
                    Remove
                  </Button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </section>
  )
}
