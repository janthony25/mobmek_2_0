import { useState } from 'react'
import { generateInvoice, getInvoices, rejectInvoice } from '@/api/invoices'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { DropdownMenu } from '@/components/ui/DropdownMenu'
import { Modal } from '@/components/ui/Modal'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { Spinner } from '@/components/ui/Spinner'
import { StateMessage } from '@/components/ui/StateMessage'
import { useToast } from '@/components/ui/toast'
import { useAsync } from '@/hooks/useAsync'
import { currency, date, percent } from '@/lib/format'
import { invoiceStatusLabel, invoiceStatusTone } from '@/lib/badges'
import { MarkPaidForm } from '@/components/invoices/MarkPaidForm'
import { EmailComposeModal } from '@/components/email/EmailComposeModal'
import { EmailStatusBadge } from '@/components/email/EmailStatusBadge'
import { Field, controlClass } from '@/components/forms/controls'
import type { CreateInvoiceRequest, Invoice } from '@/types'

interface InvoicesSectionProps {
  jobId: string
  /** Bump to force a reload, e.g. after accepting a quotation creates an invoice. */
  reloadKey?: number
}

export function InvoicesSection({ jobId, reloadKey = 0 }: InvoicesSectionProps) {
  const toast = useToast()
  // Quotations share the invoices endpoint but live in their own QuotationsSection.
  const { data, loading, error, reload } = useAsync(
    () => getInvoices(jobId).then((list) => list.filter((inv) => inv.documentType !== 'Quotation')),
    [jobId, reloadKey],
  )
  const [generating, setGenerating] = useState(false)
  const [rejecting, setRejecting] = useState<Invoice | null>(null)
  const [paying, setPaying] = useState<Invoice | null>(null)
  const [emailing, setEmailing] = useState<Invoice | null>(null)
  // Collapsed until data proves there's something to show, unless the user has toggled it.
  const [collapsedOverride, setCollapsedOverride] = useState<boolean | null>(null)
  const collapsed = collapsedOverride ?? (data == null || data.length === 0)
  // Only the very first fetch (no data yet) blocks the section body; a refetch after
  // that (search/reload) keeps the existing rows visible with a small inline spinner.
  const refreshing = loading && data != null

  const handleReject = async () => {
    if (!rejecting) return
    await rejectInvoice(jobId, rejecting.id)
    toast.success('Invoice rejected')
    setRejecting(null)
    reload()
  }

  return (
    <section className="rounded-xl border border-slate-200 border-l-4 border-l-slate-900 bg-white p-5 shadow-md">
      <div className={`flex items-end justify-between gap-4 ${collapsed ? '' : 'mb-4'}`}>
        <button
          type="button"
          onClick={() => setCollapsedOverride(!collapsed)}
          aria-expanded={!collapsed}
          className="flex items-center gap-2"
        >
          <span aria-hidden className="text-2xl">🧾</span>
          <h2 className="text-xl font-bold text-slate-900">Invoices</h2>
          <span aria-hidden className="text-sm text-slate-400">{collapsed ? '▸' : '▾'}</span>
          {!collapsed && refreshing && <Spinner className="h-4 w-4 text-slate-400" />}
        </button>
        <Button onClick={() => setGenerating(true)}>+ Generate Invoice</Button>
      </div>

      {!collapsed && loading && data == null && <StateMessage title="Loading invoices…" loading />}
      {!collapsed && error && <StateMessage title="Could not load invoices" description={error.message} />}
      {!collapsed && data && data.length === 0 && (
        <StateMessage
          title="No invoices yet"
          description="Generate one to snapshot the job's items, labour and services."
        />
      )}

      {!collapsed && data && data.length > 0 && (
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
                <th className="px-4 py-3">Email</th>
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
                    <td className="px-4 py-3 align-top">
                      <EmailStatusBadge status={inv.latestEmailStatus} />
                    </td>
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
                            onClick: () => setEmailing(inv),
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
            invoice={paying}
            onDone={() => {
              setPaying(null)
              reload()
            }}
            onCancel={() => setPaying(null)}
          />
        )}
      </Modal>

      <Modal open={emailing !== null} title="Email Invoice" onClose={() => setEmailing(null)} maxWidth="max-w-xl">
        {emailing && (
          <EmailComposeModal
            jobId={jobId}
            invoice={emailing}
            onSent={() => {
              reload()
            }}
            onClose={() => {
              setEmailing(null)
              reload()
            }}
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
          className={controlClass}
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
