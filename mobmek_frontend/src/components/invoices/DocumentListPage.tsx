import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { getInvoicesPaged, rejectInvoice } from '@/api/invoices'
import type { InvoicePagedFilters } from '@/api/invoices'
import { CrudSection } from '@/components/crud/CrudSection'
import { Badge } from '@/components/ui/Badge'
import { DateRangeFilter } from '@/components/ui/DateRangeFilter'
import { DropdownMenu } from '@/components/ui/DropdownMenu'
import { Modal } from '@/components/ui/Modal'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { useToast } from '@/components/ui/toast'
import { MarkPaidForm } from './MarkPaidForm'
import { AcceptQuotationForm } from './AcceptQuotationForm'
import { currency, date, orDash } from '@/lib/format'
import { invoiceStatusLabel, invoiceStatusTone, quotationStatusLabel, quotationStatusTone } from '@/lib/badges'
import type { InvoiceListItem } from '@/types'

interface DocumentListPageProps {
  documentType: 'Invoice' | 'Quotation'
}

/**
 * Shared list for the global Invoices and Quotations pages: a paginated, searchable
 * index across all jobs, with the same accept/reject/pay/print actions available on
 * the job detail page also reachable here via a per-row Actions dropdown.
 */
export function DocumentListPage({ documentType }: DocumentListPageProps) {
  const navigate = useNavigate()
  const toast = useToast()
  const isQuotation = documentType === 'Quotation'
  const statusLabel = isQuotation ? quotationStatusLabel : invoiceStatusLabel
  const statusTone = isQuotation ? quotationStatusTone : invoiceStatusTone
  // So the job page's back-link can return here instead of defaulting to Job Center.
  const backState = { from: isQuotation ? '/quotations' : '/invoices', fromLabel: isQuotation ? 'Quotations' : 'Invoices' }

  const [reloadKey, setReloadKey] = useState(0)
  const [rejecting, setRejecting] = useState<InvoiceListItem | null>(null)
  const [paying, setPaying] = useState<InvoiceListItem | null>(null)
  const [accepting, setAccepting] = useState<InvoiceListItem | null>(null)

  const [sortBy, setSortBy] = useState<NonNullable<InvoicePagedFilters['sortBy']>>('newest')
  // Quotation statuses: Active/Accepted/Rejected. Invoice statuses shown to users are
  // Unpaid/Paid/Rejected, derived from the underlying status+isPaid (see lib/badges.ts) —
  // mapped back to the right status/isPaid query params in `statusFilterParams` below.
  const [statusFilter, setStatusFilter] = useState('')
  const [dateFrom, setDateFrom] = useState('')
  const [dateTo, setDateTo] = useState('')

  const statusFilterParams: Pick<InvoicePagedFilters, 'status' | 'isPaid'> = isQuotation
    ? { status: statusFilter || undefined }
    : statusFilter === 'Unpaid'
      ? { status: 'Active', isPaid: false }
      : statusFilter === 'Paid'
        ? { isPaid: true }
        : statusFilter === 'Rejected'
          ? { status: 'Rejected' }
          : {}

  const hasActiveFilters = sortBy !== 'newest' || statusFilter !== '' || dateFrom !== '' || dateTo !== ''
  const clearFilters = () => {
    setSortBy('newest')
    setStatusFilter('')
    setDateFrom('')
    setDateTo('')
  }

  const handleReject = async () => {
    if (!rejecting) return
    await rejectInvoice(rejecting.jobId, rejecting.id)
    toast.success(`${documentType} rejected`)
    setRejecting(null)
    setReloadKey((k) => k + 1)
  }

  return (
    <>
      <div className="mb-3 flex flex-wrap items-center gap-2">
        <select
          value={sortBy}
          onChange={(e) => setSortBy(e.target.value as NonNullable<InvoicePagedFilters['sortBy']>)}
          className="rounded-md border border-slate-300 bg-white px-2 py-1 text-sm text-slate-700 focus:border-slate-500 focus:outline-none"
        >
          <option value="newest">Newest first</option>
          <option value="oldest">Oldest first</option>
          <option value="amountDesc">Amount (high to low)</option>
          <option value="amountAsc">Amount (low to high)</option>
        </select>
        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          className="rounded-md border border-slate-300 bg-white px-2 py-1 text-sm text-slate-700 focus:border-slate-500 focus:outline-none"
        >
          <option value="">All statuses</option>
          {isQuotation ? (
            <>
              <option value="Active">Active</option>
              <option value="Accepted">Accepted</option>
              <option value="Rejected">Rejected</option>
            </>
          ) : (
            <>
              <option value="Unpaid">Unpaid</option>
              <option value="Paid">Paid</option>
              <option value="Rejected">Rejected</option>
            </>
          )}
        </select>
        <DateRangeFilter dateFrom={dateFrom} dateTo={dateTo} onDateFromChange={setDateFrom} onDateToChange={setDateTo} />
        {hasActiveFilters && (
          <button type="button" onClick={clearFilters} className="text-xs text-slate-500 hover:underline">
            Clear filters
          </button>
        )}
      </div>

      <CrudSection<InvoiceListItem>
        resourceName={documentType}
        title={isQuotation ? 'Quotations' : 'Invoices'}
        description={
          isQuotation
            ? 'Quotations generated from jobs, across all customers.'
            : 'Invoices generated from jobs, across all customers.'
        }
        loadPaged={({ page, pageSize, search }) =>
          getInvoicesPaged(documentType, page, pageSize, search, {
            sortBy,
            ...statusFilterParams,
            dateFrom: dateFrom || undefined,
            dateTo: dateTo || undefined,
          })
        }
        reloadKey={`${reloadKey}-${sortBy}-${statusFilter}-${dateFrom}-${dateTo}`}
        pageSize={20}
        getId={(i) => i.id}
        rowLabel={(i) => i.invoiceNumber}
        emptyText={`No ${documentType.toLowerCase()}s yet`}
        columns={[
          {
            header: 'Number',
            cell: (i) => (
              <Link to={`/jobs/${i.jobId}`} state={backState} className="font-medium text-slate-900 hover:underline">
                {i.invoiceNumber}
              </Link>
            ),
          },
          { header: 'Job', cell: (i) => orDash(i.issueName) },
          { header: 'Customer', cell: (i) => orDash(i.customerName) },
          { header: 'Vehicle', cell: (i) => orDash(i.carDescription) },
          { header: 'Issued', cell: (i) => date(i.createdAtUtc) },
          { header: isQuotation ? 'Valid until' : 'Due', cell: (i) => date(i.dueDate) },
          { header: 'Total', cell: (i) => currency(i.totalAmount) },
          {
            header: 'Status',
            cell: (i) => <Badge tone={statusTone(i)}>{statusLabel(i)}</Badge>,
          },
          {
            header: 'Actions',
            className: 'text-right',
            cell: (i) => {
              const pdfUrl = `/jobs/${i.jobId}/invoices/${i.id}/pdf`
              // Quotations move Active -> Accepted/Rejected; invoices move Active -> Rejected.
              const active = i.status === 'Active'
              return (
                <DropdownMenu
                  label="Actions"
                  items={[
                    { label: 'View Job', onClick: () => navigate(`/jobs/${i.jobId}`, { state: backState }) },
                    {
                      label: `View ${documentType} (PDF)`,
                      onClick: () => window.open(pdfUrl, '_blank'),
                    },
                    {
                      label: `Download ${documentType} (PDF)`,
                      onClick: () => window.open(`${pdfUrl}?autoprint=1`, '_blank'),
                    },
                    ...(isQuotation
                      ? [{ label: 'Accept', disabled: !active, onClick: () => setAccepting(i) }]
                      : [{ label: 'Mark as Paid', disabled: !active || i.isPaid, onClick: () => setPaying(i) }]),
                    { label: 'Send Email', disabled: true, hint: 'Coming soon' },
                    { label: 'Reject', disabled: !active, tone: 'danger' as const, onClick: () => setRejecting(i) },
                  ]}
                />
              )
            },
          },
        ]}
      />

      <Modal open={paying !== null} title="Mark Invoice Paid" onClose={() => setPaying(null)}>
        {paying && (
          <MarkPaidForm
            invoice={paying}
            onDone={() => {
              setPaying(null)
              setReloadKey((k) => k + 1)
            }}
            onCancel={() => setPaying(null)}
          />
        )}
      </Modal>

      <Modal open={accepting !== null} title="Accept Quotation" onClose={() => setAccepting(null)}>
        {accepting && (
          <AcceptQuotationForm
            quotation={accepting}
            onDone={() => {
              setAccepting(null)
              setReloadKey((k) => k + 1)
            }}
            onCancel={() => setAccepting(null)}
          />
        )}
      </Modal>

      <ConfirmDialog
        open={rejecting !== null}
        title={`Reject ${documentType.toLowerCase()}`}
        message={
          rejecting
            ? `Reject “${rejecting.issueName}”? It stays on record but is marked rejected.`
            : ''
        }
        confirmLabel="Reject"
        onConfirm={handleReject}
        onCancel={() => setRejecting(null)}
      />
    </>
  )
}
