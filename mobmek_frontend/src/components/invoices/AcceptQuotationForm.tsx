import { useState } from 'react'
import { acceptQuotation } from '@/api/invoices'
import { Button } from '@/components/ui/Button'
import { useToast } from '@/components/ui/toast'
import { currency } from '@/lib/format'
import { Field, controlClass } from '@/components/forms/controls'
import type { AcceptQuotationRequest } from '@/types'

/** The subset of a quotation this form needs — satisfied by both `Invoice` and `InvoiceListItem`. */
export interface AcceptableQuotation {
  id: string
  jobId: string
  issueName: string
  totalAmount: number
}

/** "Accept Quotation" form, shared by the job detail page and the global Quotations list. */
export function AcceptQuotationForm({
  quotation,
  onDone,
  onCancel,
}: {
  quotation: AcceptableQuotation
  onDone: (invoiceNumber: string) => void
  onCancel: () => void
}) {
  const toast = useToast()
  const [dueDate, setDueDate] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    const body: AcceptQuotationRequest = {
      dueDate: dueDate || null,
    }
    try {
      const invoice = await acceptQuotation(quotation.jobId, quotation.id, body)
      toast.success(`Quotation accepted — invoice ${invoice.invoiceNumber} created`)
      onDone(invoice.invoiceNumber)
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} className="space-y-4">
      <p className="text-sm text-slate-500">
        Accepting “{quotation.issueName}” ({currency(quotation.totalAmount)}) turns it into an
        invoice with the same lines and totals — the customer pays exactly what they accepted,
        even if the job changed since. The quotation stays on record as accepted.
      </p>
      <Field label="Invoice due date">
        <input type="date" value={dueDate} onChange={(e) => setDueDate(e.target.value)} className={controlClass} />
      </Field>
      {error && <p className="text-sm text-red-600">{error}</p>}
      <div className="flex justify-end gap-2">
        <Button type="button" variant="secondary" onClick={onCancel} disabled={busy}>
          Cancel
        </Button>
        <Button type="submit" disabled={busy}>
          {busy ? 'Accepting…' : 'Accept & Create Invoice'}
        </Button>
      </div>
    </form>
  )
}
