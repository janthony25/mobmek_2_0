import { Fragment, useState } from 'react'
import { generateInvoice, getInvoices, markInvoicePaid, rejectInvoice } from '@/api/invoices'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { StateMessage } from '@/components/ui/StateMessage'
import { useToast } from '@/components/ui/toast'
import { useAsync } from '@/hooks/useAsync'
import { currency, date, orDash, percent } from '@/lib/format'
import { invoiceStatusLabel, invoiceStatusTone } from '@/lib/badges'
import type { CreateInvoiceRequest, Invoice, MarkInvoicePaidRequest } from '@/types'

export function InvoicesSection({ jobId }: { jobId: string }) {
  const toast = useToast()
  const { data, loading, error, reload } = useAsync(() => getInvoices(jobId), [jobId])
  const [generating, setGenerating] = useState(false)
  const [rejecting, setRejecting] = useState<Invoice | null>(null)
  const [paying, setPaying] = useState<Invoice | null>(null)
  const [expanded, setExpanded] = useState<Set<string>>(new Set())

  const toggle = (id: string) =>
    setExpanded((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })

  const handleReject = async () => {
    if (!rejecting) return
    await rejectInvoice(jobId, rejecting.id)
    toast.success('Invoice rejected')
    setRejecting(null)
    reload()
  }

  return (
    <section>
      <div className="mb-4 flex items-end justify-between gap-4">
        <h2 className="text-lg font-semibold text-slate-900">Invoices</h2>
        <Button onClick={() => setGenerating(true)}>+ Generate Invoice</Button>
      </div>

      {loading && <StateMessage title="Loading invoices…" />}
      {error && <StateMessage title="Could not load invoices" description={error.message} />}
      {data && data.length === 0 && (
        <StateMessage
          title="No invoices yet"
          description="Generate one to snapshot the job's items, labour and services."
        />
      )}

      {data && data.length > 0 && (
        <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white">
          <table className="min-w-full divide-y divide-slate-200 text-sm">
            <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-4 py-3">Invoice</th>
                <th className="px-4 py-3">Created</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3">Subtotal</th>
                <th className="px-4 py-3">GST</th>
                <th className="px-4 py-3">Total</th>
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {data.map((inv) => (
                <Fragment key={inv.id}>
                  <tr className="hover:bg-slate-50">
                    <td className="px-4 py-3 align-top font-medium text-slate-900">{inv.issueName}</td>
                    <td className="px-4 py-3 align-top text-slate-600">{date(inv.createdAtUtc)}</td>
                    <td className="px-4 py-3 align-top">
                      <Badge tone={invoiceStatusTone(inv)}>{invoiceStatusLabel(inv)}</Badge>
                    </td>
                    <td className="px-4 py-3 align-top text-slate-600">{currency(inv.subTotal)}</td>
                    <td className="px-4 py-3 align-top text-slate-600">
                      {currency(inv.taxAmount)}{' '}
                      <span className="text-xs text-slate-400">({percent(inv.gstRate)} incl.)</span>
                    </td>
                    <td className="px-4 py-3 align-top font-medium text-slate-900">{currency(inv.totalAmount)}</td>
                    <td className="whitespace-nowrap px-4 py-3 text-right align-top">
                      <Button variant="ghost" size="sm" onClick={() => toggle(inv.id)}>
                        {expanded.has(inv.id) ? 'Hide' : 'View'}
                      </Button>
                      {inv.status !== 'Rejected' && !inv.isPaid && (
                        <Button variant="ghost" size="sm" onClick={() => setPaying(inv)}>
                          Mark Paid
                        </Button>
                      )}
                      {inv.status !== 'Rejected' && (
                        <Button
                          variant="ghost"
                          size="sm"
                          className="text-red-600"
                          onClick={() => setRejecting(inv)}
                        >
                          Reject
                        </Button>
                      )}
                    </td>
                  </tr>
                  {expanded.has(inv.id) && (
                    <tr className="bg-slate-50/60">
                      <td colSpan={7} className="px-4 py-3">
                        <InvoiceLines invoice={inv} />
                      </td>
                    </tr>
                  )}
                </Fragment>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <Modal open={generating} title="Generate Invoice" onClose={() => setGenerating(false)}>
        <GenerateForm
          jobId={jobId}
          onDone={() => {
            setGenerating(false)
            reload()
          }}
          onCancel={() => setGenerating(false)}
        />
      </Modal>

      <Modal open={paying !== null} title="Mark Invoice Paid" onClose={() => setPaying(null)}>
        {paying && (
          <MarkPaidForm
            jobId={jobId}
            invoice={paying}
            onDone={() => {
              setPaying(null)
              reload()
            }}
            onCancel={() => setPaying(null)}
          />
        )}
      </Modal>

      <ConfirmDialog
        open={rejecting !== null}
        title="Reject invoice"
        message={rejecting ? `Reject “${rejecting.issueName}”? It stays on record but is marked rejected.` : ''}
        confirmLabel="Reject"
        onConfirm={handleReject}
        onCancel={() => setRejecting(null)}
      />
    </section>
  )
}

function InvoiceLines({ invoice }: { invoice: Invoice }) {
  return (
    <div className="space-y-3">
      <table className="min-w-full text-sm">
        <thead className="text-left text-xs font-semibold uppercase tracking-wide text-slate-400">
          <tr>
            <th className="py-1 pr-4">Line</th>
            <th className="py-1 pr-4">Qty</th>
            <th className="py-1 pr-4">Unit</th>
            <th className="py-1">Total</th>
          </tr>
        </thead>
        <tbody>
          {invoice.items.map((line) => (
            <tr key={line.id}>
              <td className="py-1 pr-4 text-slate-700">{line.itemName}</td>
              <td className="py-1 pr-4 text-slate-600">{line.quantity}</td>
              <td className="py-1 pr-4 text-slate-600">{currency(line.itemPrice)}</td>
              <td className="py-1 text-slate-600">{currency(line.itemTotal)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <dl className="grid grid-cols-2 gap-x-8 gap-y-1 text-xs text-slate-500 sm:grid-cols-4">
        <Meta label="Labour" value={currency(invoice.labourPrice)} />
        <Meta label="Due date" value={date(invoice.dueDate)} />
        {invoice.notes && <Meta label="Notes" value={invoice.notes} />}
        {invoice.isPaid && (
          <>
            <Meta label="Date paid" value={date(invoice.datePaid)} />
            <Meta label="Amount paid" value={currency(invoice.amountPaid ?? 0)} />
            <Meta label="Payment term" value={orDash(invoice.paymentTerm)} />
            <Meta label="Mode of payment" value={orDash(invoice.modeOfPayment)} />
            {invoice.cashAmount != null && <Meta label="Cash" value={currency(invoice.cashAmount)} />}
            {invoice.cardAmount != null && <Meta label="Card" value={currency(invoice.cardAmount)} />}
          </>
        )}
      </dl>
    </div>
  )
}

function Meta({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="uppercase tracking-wide text-slate-400">{label}</dt>
      <dd className="text-slate-600">{value}</dd>
    </div>
  )
}

function GenerateForm({
  jobId,
  onDone,
  onCancel,
}: {
  jobId: string
  onDone: () => void
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
    const body: CreateInvoiceRequest = {
      dueDate: dueDate || null,
    }
    try {
      await generateInvoice(jobId, body)
      toast.success('Invoice generated')
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
        Lines and totals are built automatically from the job's items, labour and services. Mode
        of payment and payment term are recorded when the invoice is marked paid.
      </p>
      <Field label="Due date">
        <input
          type="date"
          value={dueDate}
          onChange={(e) => setDueDate(e.target.value)}
          className={inputClass}
        />
      </Field>
      {error && <p className="text-sm text-red-600">{error}</p>}
      <div className="flex justify-end gap-2">
        <Button type="button" variant="secondary" onClick={onCancel} disabled={busy}>
          Cancel
        </Button>
        <Button type="submit" disabled={busy}>
          Generate
        </Button>
      </div>
    </form>
  )
}

function MarkPaidForm({
  jobId,
  invoice,
  onDone,
  onCancel,
}: {
  jobId: string
  invoice: Invoice
  onDone: () => void
  onCancel: () => void
}) {
  const toast = useToast()
  const [modeOfPayment, setModeOfPayment] = useState('')
  const [paymentTerm, setPaymentTerm] = useState('')
  const [cashAmount, setCashAmount] = useState('')
  const [cardAmount, setCardAmount] = useState('')
  const [datePaid, setDatePaid] = useState(() => new Date().toISOString().slice(0, 10))
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    const body: MarkInvoicePaidRequest = {
      modeOfPayment: modeOfPayment.trim() || null,
      paymentTerm: paymentTerm.trim() || null,
      cashAmount: cashAmount.trim() === '' ? null : Number(cashAmount),
      cardAmount: cardAmount.trim() === '' ? null : Number(cardAmount),
      datePaid: datePaid || null,
    }
    try {
      await markInvoicePaid(jobId, invoice.id, body)
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
        Recording payment for “{invoice.issueName}” — total {currency(invoice.totalAmount)}. All
        fields are optional.
      </p>
      <Field label="Date paid">
        <input
          type="date"
          value={datePaid}
          onChange={(e) => setDatePaid(e.target.value)}
          className={inputClass}
        />
      </Field>
      <Field label="Mode of payment">
        <input
          type="text"
          value={modeOfPayment}
          onChange={(e) => setModeOfPayment(e.target.value)}
          placeholder="e.g. Card"
          className={inputClass}
        />
      </Field>
      <Field label="Payment term">
        <input
          type="text"
          value={paymentTerm}
          onChange={(e) => setPaymentTerm(e.target.value)}
          placeholder="e.g. Net 14"
          className={inputClass}
        />
      </Field>
      <div className="grid grid-cols-2 gap-4">
        <Field label="Cash amount">
          <input
            type="number"
            step="0.01"
            min="0"
            value={cashAmount}
            onChange={(e) => setCashAmount(e.target.value)}
            className={inputClass}
          />
        </Field>
        <Field label="Card amount">
          <input
            type="number"
            step="0.01"
            min="0"
            value={cardAmount}
            onChange={(e) => setCardAmount(e.target.value)}
            className={inputClass}
          />
        </Field>
      </div>
      {error && <p className="text-sm text-red-600">{error}</p>}
      <div className="flex justify-end gap-2">
        <Button type="button" variant="secondary" onClick={onCancel} disabled={busy}>
          Cancel
        </Button>
        <Button type="submit" disabled={busy}>
          Mark Paid
        </Button>
      </div>
    </form>
  )
}

const inputClass =
  'w-full rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500'

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="mb-1 block text-sm font-medium text-slate-700">{label}</span>
      {children}
    </label>
  )
}
