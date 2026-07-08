import { useEffect, useState } from 'react'
import { markInvoicePaid } from '@/api/invoices'
import { Button } from '@/components/ui/Button'
import { useToast } from '@/components/ui/toast'
import { currency } from '@/lib/format'
import { Field, controlClass } from '@/components/forms/controls'
import type { MarkInvoicePaidRequest } from '@/types'

const MODES_OF_PAYMENT = ['Card', 'Cash', 'Cash & Card'] as const
type ModeOfPayment = (typeof MODES_OF_PAYMENT)[number]

/** The subset of an invoice/quotation this form needs — satisfied by both `Invoice` and `InvoiceListItem`. */
export interface PayableInvoice {
  id: string
  jobId: string
  issueName: string
  totalAmount: number
}

const round2 = (n: number) => Math.round(n * 100) / 100

/** "Mark Invoice Paid" form, shared by the job detail page and the global Invoices list. */
export function MarkPaidForm({
  invoice,
  onDone,
  onCancel,
}: {
  invoice: PayableInvoice
  onDone: () => void
  onCancel: () => void
}) {
  const toast = useToast()
  const total = round2(invoice.totalAmount)
  const [mode, setMode] = useState<ModeOfPayment>('Card')
  const [paymentTerm, setPaymentTerm] = useState('')
  const [cashAmount, setCashAmount] = useState('')
  const [cardAmount, setCardAmount] = useState('')
  const [datePaid, setDatePaid] = useState(() => new Date().toISOString().slice(0, 10))
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Single-method payments are always the full total; only a Cash & Card split needs manual entry.
  useEffect(() => {
    if (mode === 'Card') {
      setCardAmount(String(total))
      setCashAmount('')
    } else if (mode === 'Cash') {
      setCashAmount(String(total))
      setCardAmount('')
    } else {
      setCashAmount('')
      setCardAmount('')
    }
  }, [mode, total])

  const splitSum = round2((Number(cashAmount) || 0) + (Number(cardAmount) || 0))
  const splitValid = mode !== 'Cash & Card' || splitSum === total
  const remaining = round2(total - splitSum)

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!splitValid) {
      setError(`Cash + Card must equal the total (${currency(total)}).`)
      return
    }
    setBusy(true)
    setError(null)
    const body: MarkInvoicePaidRequest = {
      modeOfPayment: mode,
      paymentTerm: paymentTerm.trim() || null,
      cashAmount: cashAmount.trim() === '' ? null : Number(cashAmount),
      cardAmount: cardAmount.trim() === '' ? null : Number(cardAmount),
      datePaid: datePaid || null,
    }
    try {
      await markInvoicePaid(invoice.jobId, invoice.id, body)
      toast.success('Invoice marked paid')
      onDone()
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} className="space-y-4">
      <p className="text-sm text-slate-500">
        Recording payment for “{invoice.issueName}” — total {currency(total)}.
      </p>
      <Field label="Date paid">
        <input type="date" value={datePaid} onChange={(e) => setDatePaid(e.target.value)} className={controlClass} />
      </Field>
      <Field label="Mode of payment">
        <select value={mode} onChange={(e) => setMode(e.target.value as ModeOfPayment)} className={controlClass}>
          {MODES_OF_PAYMENT.map((m) => (
            <option key={m} value={m}>
              {m}
            </option>
          ))}
        </select>
      </Field>
      <Field label="Payment term">
        <input
          type="text"
          value={paymentTerm}
          onChange={(e) => setPaymentTerm(e.target.value.toUpperCase())}
          placeholder="e.g. Net 14"
          className={controlClass}
        />
      </Field>

      {mode === 'Cash & Card' ? (
        <>
          <div className="grid grid-cols-2 gap-4">
            <Field label="Cash amount">
              <input
                type="number"
                step="0.01"
                min="0"
                value={cashAmount}
                onChange={(e) => setCashAmount(e.target.value)}
                className={controlClass}
              />
            </Field>
            <Field label="Card amount">
              <input
                type="number"
                step="0.01"
                min="0"
                value={cardAmount}
                onChange={(e) => setCardAmount(e.target.value)}
                className={controlClass}
              />
            </Field>
          </div>
          <p className={`text-sm ${splitValid ? 'text-slate-500' : 'text-red-600'}`}>
            {splitValid
              ? `Cash + Card = ${currency(splitSum)}, matches the total.`
              : remaining > 0
                ? `${currency(remaining)} short of the ${currency(total)} total.`
                : `${currency(-remaining)} over the ${currency(total)} total.`}
          </p>
        </>
      ) : (
        <Field label={mode === 'Card' ? 'Card amount' : 'Cash amount'}>
          <input type="text" value={currency(total)} disabled className={`${controlClass} bg-slate-50 text-slate-500`} />
        </Field>
      )}

      {error && <p className="text-sm text-red-600">{error}</p>}
      <div className="flex justify-end gap-2">
        <Button type="button" variant="secondary" onClick={onCancel} disabled={busy}>
          Cancel
        </Button>
        <Button type="submit" disabled={busy || !splitValid}>
          Mark Paid
        </Button>
      </div>
    </form>
  )
}
