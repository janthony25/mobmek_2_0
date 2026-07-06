import { Button } from '@/components/ui/Button'
import { Field, controlClass } from '@/components/forms/controls'
import { currency } from '@/lib/format'
import { computeDiscountAmount } from '@/lib/jobLineDrafts'
import { DiscountType } from '@/types'

interface DiscountEditorProps {
  discountType: DiscountType
  discountValue: string
  /** Pre-discount total of items + labour + services, used to preview the discount amount. */
  subtotal: number
  onAdd: () => void
  onChange: (patch: { discountType?: DiscountType; discountValue?: string }) => void
  onRemove: () => void
}

/** A single optional job-level discount, editable as either $ or %. */
export function DiscountEditor({ discountType, discountValue, subtotal, onAdd, onChange, onRemove }: DiscountEditorProps) {
  const hasDiscount = discountType !== DiscountType.None

  return (
    <section className="rounded-lg border border-slate-200 bg-white p-5">
      <div className="mb-3 flex items-center justify-between">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">Discount</h2>
        {!hasDiscount && (
          <Button type="button" variant="secondary" size="sm" onClick={onAdd}>
            + Add discount
          </Button>
        )}
      </div>
      {!hasDiscount ? (
        <p className="text-sm text-slate-500">No discount applied.</p>
      ) : (
        <div className="rounded-md border border-slate-200 p-3">
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <Field label="Type">
              <select
                value={discountType}
                onChange={(e) => onChange({ discountType: Number(e.target.value) as DiscountType })}
                className={controlClass}
              >
                <option value={DiscountType.Fixed}>$ Fixed</option>
                <option value={DiscountType.Percentage}>% Percentage</option>
              </select>
            </Field>
            <Field label={discountType === DiscountType.Percentage ? 'Percent' : 'Amount'}>
              <input
                type="number"
                step="0.01"
                min={0}
                max={discountType === DiscountType.Percentage ? 100 : undefined}
                value={discountValue}
                onChange={(e) => onChange({ discountValue: e.target.value })}
                className={controlClass}
              />
            </Field>
            <div className="flex items-end justify-between gap-2 sm:col-span-2">
              <div className="text-sm text-slate-600">
                Discount: <strong className="text-slate-900">-{currency(computeDiscountAmount(discountType, discountValue, subtotal))}</strong>
              </div>
              <Button type="button" variant="danger" size="sm" onClick={onRemove}>
                Remove
              </Button>
            </div>
          </div>
        </div>
      )}
    </section>
  )
}
