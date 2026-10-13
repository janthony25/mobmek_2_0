import { useEffect, useState } from 'react'
import { addPartialPayment, getInvoice } from '@/api/invoices'
import { Button } from '@/components/ui/Button'
import { Spinner } from '@/components/ui/Spinner'
import { useToast } from '@/components/ui/toast'
import { currency } from '@/lib/format'
import { Field, controlClass } from '@/components/forms/controls'
import { PartialPaymentHistoryList } from './PartialPaymentHistoryList'
import type { CreateInvoicePartialPaymentRequest, Invoice } from '@/types'

const MODES_OF_PAYMENT = ['Cash', 'Card', 'Bank Transfer'] as const
type ModeOfPayment = (typeof MODES_OF_PAYMENT)[number]

const round2 = (n: number) => Math.round(n * 100) / 100

/** The subset of an invoice/quotation this form needs — satisfied by both `Invoice` and `InvoiceListItem`. */
export interface PartiallyPayableInvoice {
  id: string
  jobId: string
  issueName: string
}

/**
 * "Partial Payment" wizard: shows the invoice's recorded payment history (note text editable via
 * a button, delete gated behind confirmation) above a form for recording a new one. Record-
 * keeping only — no cash-flow ledger entry yet, and it never touches Invoice.Notes or the job's
 * notes (those print on the customer-facing PDF/job form).
 */
export function PartialPaymentForm({
  invoice,
  onDone,
  onCancel,
}: {
  invoice: PartiallyPayableInvoice
  onDone: () => void
  onCancel: () => void
}) {
  const toast = useToast()
  const [detail, setDetail] = useState<Invoice | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)

  const load = async () => {
    setLoading(true)
    try {
      setDetail(await getInvoice(invoice.jobId, invoice.id))
      setLoadError(null)
    } catch (err) {
      setLoadError(err instanceof Error ? err.message : String(err))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [invoice.jobId, invoice.id])

  if (loading) {
    return (
      <p className="flex items-center gap-2 text-sm text-slate-500">
        <Spinner className="h-3.5 w-3.5" /> Loading…
      </p>
    )
  }

  if (loadError || !detail) {
    return <p className="text-sm text-red-600">{loadError ?? 'Could not load invoice.'}</p>
  }

  return (
    <div className="space-y-5">
      <PartialPaymentHistoryList
        entries={detail.partialPayments.map((payment) => ({ payment, jobId: detail.jobId, invoiceId: detail.id }))}
        onChanged={load}
      />
      <NewPartialPaymentForm
        invoice={detail}
        onRecorded={() => {
          toast.success('Partial payment recorded')
          onDone()
        }}
        onCancel={onCancel}
      />
    </div>
  )
}

function NewPartialPaymentForm({
  invoice,
  onRecorded,
  onCancel,
}: {
  invoice: Invoice
  onRecorded: () => void
  onCancel: () => void
}) {
  const remaining = round2(invoice.remainingBalance)
  const [mode, setMode] = useState<ModeOfPayment>('Cash')
  const [amount, setAmount] = useState('')
  const [paymentDate, setPaymentDate] = useState(() => new Date().toISOString().slice(0, 10))
  const [notes, setNotes] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const amountValue = Number(amount) || 0
  const amountValid = amount.trim() !== '' && amountValue > 0 && amountValue <= remaining
  const newRemaining = round2(remaining - amountValue)

  if (remaining <= 0) {
    return <p className="text-sm text-slate-500">This invoice has no remaining balance left to record.</p>
  }

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!amountValid) {
      setError(`Amount must be greater than $0 and no more than the remaining balance (${currency(remaining)}).`)
      return
    }
    setBusy(true)
    setError(null)
    const body: CreateInvoicePartialPaymentRequest = {
      amount: amountValue,
      modeOfPayment: mode,
      notes: notes.trim() || null,
      paymentDate: paymentDate || null,
    }
    try {
      await addPartialPayment(invoice.jobId, invoice.id, body)
      onRecorded()
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} className="space-y-4">
      <p className="text-sm text-slate-500">
        Recording a partial payment for “{invoice.issueName}” — remaining balance {currency(remaining)}.
      </p>
      <Field label="Mode of payment">
        <select value={mode} onChange={(e) => setMode(e.target.value as ModeOfPayment)} className={controlClass}>
          {MODES_OF_PAYMENT.map((m) => (
            <option key={m} value={m}>
              {m}
            </option>
          ))}
        </select>
      </Field>
      <Field label="Amount">
        <input
          type="number"
          step="0.01"
          min="0"
          max={remaining}
          value={amount}
          onChange={(e) => setAmount(e.target.value)}
          className={controlClass}
        />
      </Field>
      <Field label="Date paid">
        <input type="date" value={paymentDate} onChange={(e) => setPaymentDate(e.target.value)} className={controlClass} />
      </Field>
      <Field label="Remaining balance after this payment">
        <input
          type="text"
          value={currency(amountValid ? newRemaining : remaining)}
          disabled
          className={`${controlClass} bg-slate-50 text-slate-500`}
        />
      </Field>
      <Field label="Notes">
        <textarea
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
          rows={3}
          placeholder="e.g. deposit for parts"
          className={controlClass}
        />
      </Field>

      {error && <p className="text-sm text-red-600">{error}</p>}
      <div className="flex justify-end gap-2">
        <Button type="button" variant="secondary" onClick={onCancel} disabled={busy}>
          Cancel
        </Button>
        <Button type="submit" disabled={busy || !amountValid}>
          Record Payment
        </Button>
      </div>
    </form>
  )
}
