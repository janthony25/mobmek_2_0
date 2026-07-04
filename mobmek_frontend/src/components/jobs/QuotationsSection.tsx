import { useState } from 'react'
import { generateQuotation, getInvoices, rejectInvoice } from '@/api/invoices'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { DropdownMenu } from '@/components/ui/DropdownMenu'
import { Modal } from '@/components/ui/Modal'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { StateMessage } from '@/components/ui/StateMessage'
import { useToast } from '@/components/ui/toast'
import { useAsync } from '@/hooks/useAsync'
import { currency, date, percent } from '@/lib/format'
import { quotationStatusLabel, quotationStatusTone } from '@/lib/badges'
import { AcceptQuotationForm } from '@/components/invoices/AcceptQuotationForm'
import type { CreateInvoiceRequest, Invoice } from '@/types'

interface QuotationsSectionProps {
  jobId: string
  /** Called after a quotation is accepted, so the parent can refresh the invoices list. */
  onAccepted?: () => void
}

export function QuotationsSection({ jobId, onAccepted }: QuotationsSectionProps) {
  const toast = useToast()
  // Quotations are invoices with documentType "Quotation" on the same endpoint.
  const { data, loading, error, reload } = useAsync(
    () => getInvoices(jobId).then((list) => list.filter((inv) => inv.documentType === 'Quotation')),
    [jobId],
  )
  const [generating, setGenerating] = useState(false)
  const [accepting, setAccepting] = useState<Invoice | null>(null)
  const [rejecting, setRejecting] = useState<Invoice | null>(null)
  // Collapsed until data proves there's something to show, unless the user has toggled it.
  const [collapsedOverride, setCollapsedOverride] = useState<boolean | null>(null)
  const collapsed = collapsedOverride ?? (data == null || data.length === 0)

  const handleReject = async () => {
    if (!rejecting) return
    await rejectInvoice(jobId, rejecting.id)
    toast.success('Quotation rejected')
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
          <span aria-hidden className="text-2xl">📄</span>
          <h2 className="text-xl font-bold text-slate-900">Quotations</h2>
          <span aria-hidden className="text-sm text-slate-400">{collapsed ? '▸' : '▾'}</span>
        </button>
        <Button onClick={() => setGenerating(true)}>+ Generate Quotation</Button>
      </div>

      {!collapsed && loading && <StateMessage title="Loading quotations…" />}
      {!collapsed && error && <StateMessage title="Could not load quotations" description={error.message} />}
      {!collapsed && data && data.length === 0 && (
        <StateMessage
          title="No quotations yet"
          description="Generate one to price the job's items, labour and services without issuing an invoice."
        />
      )}

      {!collapsed && data && data.length > 0 && (
        <div className="overflow-x-auto rounded-lg border border-slate-200">
          <table className="min-w-full divide-y divide-slate-200 text-sm">
            <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-4 py-3">Quotation</th>
                <th className="px-4 py-3">Created</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3">Subtotal</th>
                <th className="px-4 py-3">GST</th>
                <th className="px-4 py-3">Total</th>
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {data.map((quo) => {
                const active = quo.status === 'Active'
                return (
                  <tr key={quo.id} className="hover:bg-slate-50">
                    <td className="px-4 py-3 align-top font-medium text-slate-900">{quo.issueName}</td>
                    <td className="px-4 py-3 align-top text-slate-600">{date(quo.createdAtUtc)}</td>
                    <td className="px-4 py-3 align-top">
                      <Badge tone={quotationStatusTone(quo)}>{quotationStatusLabel(quo)}</Badge>
                    </td>
                    <td className="px-4 py-3 align-top text-slate-600">{currency(quo.subTotal)}</td>
                    <td className="px-4 py-3 align-top text-slate-600">
                      {currency(quo.taxAmount)}{' '}
                      <span className="text-xs text-slate-400">({percent(quo.gstRate)})</span>
                    </td>
                    <td className="px-4 py-3 align-top font-medium text-slate-900">{currency(quo.totalAmount)}</td>
                    <td className="whitespace-nowrap px-4 py-3 text-right align-top">
                      <DropdownMenu
                        label="Actions"
                        items={[
                          {
                            label: 'View Quotation (PDF)',
                            onClick: () => window.open(`/jobs/${jobId}/invoices/${quo.id}/pdf`, '_blank'),
                          },
                          {
                            label: 'Download Quotation (PDF)',
                            onClick: () => window.open(`/jobs/${jobId}/invoices/${quo.id}/pdf?autoprint=1`, '_blank'),
                          },
                          {
                            label: 'Print Quotation',
                            onClick: () =>
                              window.open(
                                `/jobs/${jobId}/invoices/${quo.id}/pdf?autoprint=1`,
                                '_blank',
                                'width=900,height=700',
                              ),
                          },
                          {
                            label: 'Send Email',
                            disabled: true,
                            hint: 'Coming soon',
                          },
                          {
                            label: 'Accept',
                            disabled: !active,
                            onClick: () => setAccepting(quo),
                          },
                          {
                            label: 'Reject',
                            disabled: !active,
                            tone: 'danger',
                            onClick: () => setRejecting(quo),
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

      <Modal open={generating} title="Generate Quotation" onClose={() => setGenerating(false)}>
        <GenerateForm
          jobId={jobId}
          onDone={() => {
            setGenerating(false)
            reload()
          }}
          onCancel={() => setGenerating(false)}
        />
      </Modal>

      <Modal open={accepting !== null} title="Accept Quotation" onClose={() => setAccepting(null)}>
        {accepting && (
          <AcceptQuotationForm
            quotation={accepting}
            onDone={() => {
              setAccepting(null)
              reload()
              onAccepted?.()
            }}
            onCancel={() => setAccepting(null)}
          />
        )}
      </Modal>

      <ConfirmDialog
        open={rejecting !== null}
        title="Reject quotation"
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
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    // The validity date is set by the backend: always 30 days after issue.
    const body: CreateInvoiceRequest = {
      dueDate: null,
    }
    try {
      await generateQuotation(jobId, body)
      toast.success('Quotation generated')
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
        Lines and totals are built automatically from the job's items, labour and services, just
        like an invoice — but a quotation is a price offer and can't be marked paid. It is valid
        only for 30 days after it is issued.
      </p>
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
