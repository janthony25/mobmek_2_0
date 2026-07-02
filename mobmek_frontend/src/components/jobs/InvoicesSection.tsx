import { useEffect, useState } from 'react'
import { generateInvoice, getInvoices, markInvoicePaid, rejectInvoice } from '@/api/invoices'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { DropdownMenu } from '@/components/ui/DropdownMenu'
import { Modal } from '@/components/ui/Modal'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { StateMessage } from '@/components/ui/StateMessage'
import { useToast } from '@/components/ui/toast'
import { useAsync } from '@/hooks/useAsync'
import { currency, date, percent } from '@/lib/format'
import { invoiceStatusLabel, invoiceStatusTone } from '@/lib/badges'
import type { CreateInvoiceRequest, Invoice, MarkInvoicePaidRequest } from '@/types'

const MODES_OF_PAYMENT = ['Card', 'Cash', 'Cash & Card'] as const
type ModeOfPayment = (typeof MODES_OF_PAYMENT)[number]

export function InvoicesSection({ jobId }: { jobId: string }) {
  const toast = useToast()
  // Quotations share the invoices endpoint but live in their own QuotationsSection.
  const { data, loading, error, reload } = useAsync(
    () => getInvoices(jobId).then((list) => list.filter((inv) => inv.documentType !== 'Quotation')),
    [jobId],
  )
  const [generating, setGenerating] = useState(false)
  const [rejecting, setRejecting] = useState<Invoice | null>(null)
  const [paying, setPaying] = useState<Invoice | null>(null)

  const handleReject = async () => {
    if (!rejecting) return
    await rejectInvoice(jobId, rejecting.id)
    toast.success('Invoice rejected')
    setRejecting(null)
    reload()
  }

  return (
    <section className="rounded-xl border border-slate-200 border-l-4 border-l-slate-900 bg-white p-5 shadow-md">
      <div className="mb-4 flex items-end justify-between gap-4">
        <div className="flex items-center gap-2">
          <span aria-hidden className="text-2xl">🧾</span>
          <h2 className="text-xl font-bold text-slate-900">Invoices</h2>
        </div>
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
        <div className="overflow-x-auto rounded-lg border border-slate-200">
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
              {data.map((inv) => {
                const rejected = inv.status === 'Rejected'
                return (
                  <tr key={inv.id} className="hover:bg-slate-50">
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
                      <DropdownMenu
                        label="Actions"
                        items={[
                          {
                            label: 'View Invoice (PDF)',
                            onClick: () => window.open(`/jobs/${jobId}/invoices/${inv.id}/pdf`, '_blank'),
                          },
                          {
                            label: 'Download Invoice (PDF)',
                            onClick: () => window.open(`/jobs/${jobId}/invoices/${inv.id}/pdf?autoprint=1`, '_blank'),
                          },
                          {
                            label: 'Print Invoice',
                            onClick: () =>
                              window.open(
                                `/jobs/${jobId}/invoices/${inv.id}/pdf?autoprint=1`,
                                '_blank',
                                'width=900,height=700',
                              ),
                          },
                          {
                            label: 'Mark as Paid',
                            disabled: rejected || inv.isPaid,
                            onClick: () => setPaying(inv),
                          },
                          {
                            label: 'Send Email',
                            disabled: true,
                            hint: 'Coming soon',
                          },
                          {
                            label: 'Reject',
                            disabled: rejected,
                            tone: 'danger',
                            onClick: () => setRejecting(inv),
                          },
                        ]}
                      />
                    </td>
                  </tr>
                )
              })}
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

const round2 = (n: number) => Math.round(n * 100) / 100

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
        Recording payment for “{invoice.issueName}” — total {currency(total)}.
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
        <select value={mode} onChange={(e) => setMode(e.target.value as ModeOfPayment)} className={inputClass}>
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
          onChange={(e) => setPaymentTerm(e.target.value)}
          placeholder="e.g. Net 14"
          className={inputClass}
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
          <input type="text" value={currency(total)} disabled className={`${inputClass} bg-slate-50 text-slate-500`} />
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
