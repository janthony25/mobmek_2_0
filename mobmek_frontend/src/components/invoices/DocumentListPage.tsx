import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { getInvoicesPaged, rejectInvoice } from '@/api/invoices'
import { CrudSection } from '@/components/crud/CrudSection'
import { Badge } from '@/components/ui/Badge'
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

  const [reloadKey, setReloadKey] = useState(0)
  const [rejecting, setRejecting] = useState<InvoiceListItem | null>(null)
  const [paying, setPaying] = useState<InvoiceListItem | null>(null)
  const [accepting, setAccepting] = useState<InvoiceListItem | null>(null)

  const handleReject = async () => {
    if (!rejecting) return
    await rejectInvoice(rejecting.jobId, rejecting.id)
    toast.success(`${documentType} rejected`)
    setRejecting(null)
    setReloadKey((k) => k + 1)
  }

  return (
    <>
      <CrudSection<InvoiceListItem>
        resourceName={documentType}
        title={isQuotation ? 'Quotations' : 'Invoices'}
        description={
          isQuotation
            ? 'Quotations generated from jobs, across all customers.'
            : 'Invoices generated from jobs, across all customers.'
        }
        loadPaged={({ page, pageSize, search }) => getInvoicesPaged(documentType, page, pageSize, search)}
        dateSearchButton
        reloadKey={reloadKey}
        pageSize={20}
        getId={(i) => i.id}
        rowLabel={(i) => i.invoiceNumber}
        emptyText={`No ${documentType.toLowerCase()}s yet`}
        columns={[
          {
            header: 'Number',
            cell: (i) => (
              <Link to={`/jobs/${i.jobId}`} className="font-medium text-slate-900 hover:underline">
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
                    { label: 'View Job', onClick: () => navigate(`/jobs/${i.jobId}`) },
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
