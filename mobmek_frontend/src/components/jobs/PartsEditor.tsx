import { Button } from '@/components/ui/Button'
import { Field, controlClass } from '@/components/forms/controls'
import { currency } from '@/lib/format'
import { computePart, num, type PartDraft } from '@/lib/jobLineDrafts'
import { MARKUP_SOLUTION_LABELS } from '@/types'

interface PartsEditorProps {
  parts: PartDraft[]
  onAdd: () => void
  onUpdate: (key: string, patch: Partial<PartDraft>) => void
  onRemove: (key: string) => void
}

/** Editable Parts & Items cards, shared by the New Job form and the Job Details edit mode. */
export function PartsEditor({ parts, onAdd, onUpdate, onRemove }: PartsEditorProps) {
  return (
    <section className="rounded-lg border border-slate-200 bg-white p-5">
      <div className="mb-3 flex items-center justify-between">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">Parts &amp; Items</h2>
        <Button type="button" variant="secondary" size="sm" onClick={onAdd}>
          + Add part
        </Button>
      </div>
      {parts.length === 0 ? (
        <p className="text-sm text-slate-500">No parts yet. Use “Add part” to add one.</p>
      ) : (
        <div className="space-y-3">
          {parts.map((p) => {
            const c = computePart(p)
            return (
              <div key={p.key} className="rounded-md border border-slate-200 p-3">
                <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
                  <Field label="Item name" required className="col-span-2">
                    <input value={p.itemName} onChange={(e) => onUpdate(p.key, { itemName: e.target.value })} className={controlClass} />
                  </Field>
                  <Field label="Quantity">
                    <input type="number" min={1} value={p.itemQuantity} onChange={(e) => onUpdate(p.key, { itemQuantity: e.target.value })} className={controlClass} />
                  </Field>
                  <Field label="Trade price">
                    <input type="number" step="0.01" min={0} value={p.tradePrice} onChange={(e) => onUpdate(p.key, { tradePrice: e.target.value })} className={controlClass} />
                  </Field>
                  <Field label="Retail price">
                    <input type="number" step="0.01" min={0} value={p.retailPrice} onChange={(e) => onUpdate(p.key, { retailPrice: e.target.value })} className={controlClass} />
                  </Field>
                  <Field label="Markup type">
                    <select
                      value={p.markupSolution}
                      onChange={(e) => onUpdate(p.key, { markupSolution: Number(e.target.value) })}
                      className={controlClass}
                    >
                      {Object.entries(MARKUP_SOLUTION_LABELS).map(([value, label]) => (
                        <option key={value} value={value}>
                          {label}
                        </option>
                      ))}
                    </select>
                  </Field>
                  <Field label="Markup value">
                    <input type="number" step="0.01" min={0} value={p.markup} onChange={(e) => onUpdate(p.key, { markup: e.target.value })} className={controlClass} />
                  </Field>
                  <Field label="Selling price" className="sm:col-span-2">
                    <input
                      type="number"
                      step="0.01"
                      min={0}
                      value={p.sellingPrice}
                      onChange={(e) => onUpdate(p.key, { sellingPrice: e.target.value })}
                      disabled={num(p.retailPrice) != null}
                      className={`${controlClass} ${num(p.retailPrice) != null ? 'bg-slate-50 text-slate-400' : ''}`}
                      placeholder={num(p.retailPrice) != null ? 'Derived from markup' : 'Used when no retail price'}
                    />
                  </Field>
                </div>
                <div className="mt-3 flex flex-wrap items-center justify-between gap-3 border-t border-slate-100 pt-3 text-sm">
                  <div className="flex flex-wrap gap-x-6 gap-y-1 text-slate-600">
                    <span>Unit price: <strong className="text-slate-900">{currency(c.unitPrice)}</strong></span>
                    <span>Item total: <strong className="text-slate-900">{currency(c.itemTotal)}</strong></span>
                    <span>Row profit: <strong className="text-slate-900">{currency(c.rowProfit)}</strong></span>
                  </div>
                  <Button type="button" variant="ghost" size="sm" className="text-red-600" onClick={() => onRemove(p.key)}>
                    Remove
                  </Button>
                </div>
              </div>
            )
          })}
        </div>
      )}
    </section>
  )
}
