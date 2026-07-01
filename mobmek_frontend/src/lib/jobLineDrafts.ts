import { MarkupSolution } from '@/types'

// ── numeric helpers ───────────────────────────────────────────────────────────

export const num = (s: string): number | null => {
  const t = s.trim()
  if (t === '') return null
  const n = Number(t)
  return Number.isFinite(n) ? n : null
}

export const round2 = (n: number) => (Math.sign(n) * Math.round(Math.abs(n) * 100)) / 100

export const newKey = () => Math.random().toString(36).slice(2)

// ── draft row shapes ───────────────────────────────────────────────────────────

/** `id` is set when the draft mirrors an existing backend row (edit flows); null for a new row. */
export interface PartDraft {
  key: string
  id: string | null
  itemName: string
  tradePrice: string
  retailPrice: string
  markupSolution: number
  markup: string
  sellingPrice: string
  itemQuantity: string
}

export interface LabourDraft {
  key: string
  id: string | null
  hours: string
  ratePerHour: string
  fixedAmount: string
}

export const emptyPart = (): PartDraft => ({
  key: newKey(),
  id: null,
  itemName: '',
  tradePrice: '',
  retailPrice: '',
  markupSolution: MarkupSolution.Percentage,
  markup: '0',
  sellingPrice: '',
  itemQuantity: '1',
})

export const emptyLabour = (): LabourDraft => ({ key: newKey(), id: null, hours: '', ratePerHour: '', fixedAmount: '' })

/** Mirrors the backend's JobItemService.Apply so the operator sees live figures. */
export function computePart(p: PartDraft) {
  const trade = num(p.tradePrice)
  const retail = num(p.retailPrice)
  const markup = num(p.markup) ?? 0
  const qty = num(p.itemQuantity) ?? 0
  // Selling price is the markup applied to the retail price; the trade price is the cost,
  // used only to derive profit. Without a retail price, the manual selling price is used.
  const selling = round2(
    retail != null
      ? p.markupSolution === MarkupSolution.Percentage
        ? retail * (1 + markup / 100)
        : retail + markup
      : num(p.sellingPrice) ?? 0,
  )
  const unitProfit = round2(selling - (trade ?? 0))
  return { unitPrice: selling, itemTotal: round2(selling * qty), rowProfit: round2(unitProfit * qty) }
}

export function computeLabour(l: LabourDraft) {
  const fixed = num(l.fixedAmount)
  return round2(fixed != null ? fixed : (num(l.hours) ?? 0) * (num(l.ratePerHour) ?? 0))
}
