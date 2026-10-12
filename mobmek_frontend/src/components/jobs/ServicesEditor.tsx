import { useState } from 'react'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { Field, controlClass } from '@/components/forms/controls'
import { currency } from '@/lib/format'
import { num } from '@/lib/jobLineDrafts'
import type { JobService } from '@/types'

interface ServicesEditorProps {
  catalog: JobService[]
  /**
   * Ticked services, keyed by catalog service id. The value is that line's additional-amount
   * draft string ('' means ticked with no extra amount set). A missing key means not ticked.
   */
  selected: Map<string, string>
  onToggle: (serviceId: string) => void
  onSetAmount: (serviceId: string, amount: string) => void
}

/**
 * Editable catalog-services grid, shared by the New Job form and the Job Details edit mode.
 * Some catalog services are a baseline price that can run higher on a given job (e.g. "General
 * Service: $200" but this job needs more) — ticking one reveals an "+ Add amount" control that
 * opens a popup for a one-off surcharge, folded into that service's displayed price.
 */
export function ServicesEditor({ catalog, selected, onToggle, onSetAmount }: ServicesEditorProps) {
  const [editing, setEditing] = useState<JobService | null>(null)
  const [draftAmount, setDraftAmount] = useState('')

  const openAmountPopup = (service: JobService) => {
    setEditing(service)
    setDraftAmount(selected.get(service.id) ?? '')
  }

  const saveAmount = () => {
    if (editing) onSetAmount(editing.id, draftAmount)
    setEditing(null)
  }

  if (catalog.length === 0) {
    return <p className="text-sm text-slate-500">No active catalog services. Add some under Catalog → Services.</p>
  }

  return (
    <>
      <div className="grid grid-cols-1 gap-2 sm:grid-cols-2 lg:grid-cols-3">
        {catalog.map((s) => {
          const isSelected = selected.has(s.id)
          const extra = num(selected.get(s.id) ?? '') ?? 0
          return (
            <label
              key={s.id}
              className="flex items-center gap-2 rounded-md border border-slate-300 px-3 py-2 text-sm hover:bg-slate-50"
            >
              <input type="checkbox" checked={isSelected} onChange={() => onToggle(s.id)} />
              <span className="flex-1 text-slate-700">{s.name}</span>
              {isSelected && (
                <button
                  type="button"
                  onClick={(e) => {
                    e.preventDefault()
                    openAmountPopup(s)
                  }}
                  className="text-xs font-medium text-blue-600 hover:underline"
                >
                  {extra > 0 ? `+${currency(extra)}` : '+ Add amount'}
                </button>
              )}
              <span className="text-slate-500">{currency(s.price + extra)}</span>
            </label>
          )
        })}
      </div>

      <Modal open={editing !== null} title="Add amount" onClose={() => setEditing(null)}>
        {editing && (
          <div className="space-y-4">
            <p className="text-sm text-slate-500">
              {editing.name} is listed at {currency(editing.price)}. Add an extra amount if this job's price is higher
              — the catalog price plus this amount becomes the new price.
            </p>
            <Field label="Additional amount">
              <input
                type="number"
                step="0.01"
                min={0}
                autoFocus
                value={draftAmount}
                onChange={(e) => setDraftAmount(e.target.value)}
                className={controlClass}
              />
            </Field>
            <p className="text-sm text-slate-600">
              New price:{' '}
              <strong className="text-slate-900">{currency(editing.price + (num(draftAmount) ?? 0))}</strong>
            </p>
            <div className="flex justify-end gap-2">
              <Button type="button" variant="secondary" onClick={() => setEditing(null)}>
                Cancel
              </Button>
              <Button type="button" onClick={saveAmount}>
                Save
              </Button>
            </div>
          </div>
        )}
      </Modal>
    </>
  )
}
