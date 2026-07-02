import { useEffect, useRef } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { getBusinessDetails } from '@/api/businessDetails'
import { getInvoice } from '@/api/invoices'
import { getJob } from '@/api/jobs'
import { Button } from '@/components/ui/Button'
import { StateMessage } from '@/components/ui/StateMessage'
import { useAsync } from '@/hooks/useAsync'
import { currency, date, orDash, percent } from '@/lib/format'

export function InvoicePrintPage() {
  const { jobId = '', invoiceId = '' } = useParams()
  const [searchParams] = useSearchParams()
  const autoprint = searchParams.get('autoprint') === '1'
  const hasAutoprinted = useRef(false)

  const businessQuery = useAsync(getBusinessDetails, [])
  const jobQuery = useAsync(() => getJob(jobId), [jobId])
  const invoiceQuery = useAsync(() => getInvoice(jobId, invoiceId), [jobId, invoiceId])

  const ready = businessQuery.data && jobQuery.data && invoiceQuery.data
  const anyError = businessQuery.error ?? jobQuery.error ?? invoiceQuery.error

  useEffect(() => {
    if (autoprint && ready && !hasAutoprinted.current) {
      hasAutoprinted.current = true
      // Let the layout paint before invoking the browser's print dialog.
      setTimeout(() => window.print(), 150)
    }
  }, [autoprint, ready])

  if (anyError) return <StateMessage title="Could not load invoice" description={anyError.message} />
  if (!ready) return <StateMessage title="Loading invoice…" />

  const business = businessQuery.data!
  const job = jobQuery.data!
  const invoice = invoiceQuery.data!

  return (
    <div className="min-h-screen bg-slate-100 print:bg-white">
      <div className="print:hidden sticky top-0 z-10 flex items-center justify-between border-b border-slate-200 bg-white px-6 py-3">
        <Link to={`/jobs/${jobId}`} className="text-sm text-slate-500 hover:underline">
          ← Back to job
        </Link>
        <Button onClick={() => window.print()}>Print / Save as PDF</Button>
      </div>

      <div className="mx-auto max-w-3xl bg-white p-10 shadow-sm print:max-w-none print:shadow-none">
        <header className="flex items-start justify-between gap-6 border-b border-slate-200 pb-6">
          <div>
            {business.logoUrl && (
              <img src={business.logoUrl} alt={business.name} className="mb-2 h-10 object-contain" />
            )}
            <h1 className="text-xl font-bold text-slate-900">{business.name}</h1>
            {business.address && <p className="mt-1 whitespace-pre-line text-sm text-slate-500">{business.address}</p>}
            <p className="mt-1 text-sm text-slate-500">
              {[business.businessPhone, business.telephone, business.email, business.website]
                .filter(Boolean)
                .join(' · ')}
            </p>
          </div>
          <div className="text-right">
            <h2 className="text-2xl font-bold uppercase tracking-wide text-slate-900">Invoice</h2>
            <p className="mt-1 text-sm text-slate-500">
              Invoice ID: <span className="font-medium text-slate-700">{invoice.invoiceNumber}</span>
            </p>
            {business.gstNumber && (
              <p className="text-sm text-slate-500">
                GST No: <span className="font-medium text-slate-700">{business.gstNumber}</span>
              </p>
            )}
          </div>
        </header>

        <section className="mt-6 grid grid-cols-2 gap-6">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Bill to</p>
            <p className="mt-1 text-sm font-medium text-slate-900">{orDash(job.customerName)}</p>
            <p className="text-sm text-slate-600">{orDash(job.carDescription)}</p>
          </div>
          <div className="text-right">
            <dl className="space-y-1 text-sm">
              <div className="flex justify-end gap-2">
                <dt className="text-slate-400">Issued</dt>
                <dd className="w-28 text-slate-700">{date(invoice.createdAtUtc)}</dd>
              </div>
              <div className="flex justify-end gap-2">
                <dt className="text-slate-400">Due</dt>
                <dd className="w-28 text-slate-700">{date(invoice.dueDate)}</dd>
              </div>
            </dl>
          </div>
        </section>

        <table className="mt-8 w-full text-sm">
          <thead>
            <tr className="border-b border-slate-300 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
              <th className="pb-2">Description</th>
              <th className="pb-2 text-right">Qty</th>
              <th className="pb-2 text-right">Unit price</th>
              <th className="pb-2 text-right">Total</th>
            </tr>
          </thead>
          <tbody>
            {invoice.items.map((line) => (
              <tr key={line.id} className="border-b border-slate-100">
                <td className="py-2 text-slate-800">{line.itemName}</td>
                <td className="py-2 text-right text-slate-600">{line.quantity}</td>
                <td className="py-2 text-right text-slate-600">{currency(line.itemPrice)}</td>
                <td className="py-2 text-right text-slate-800">{currency(line.itemTotal)}</td>
              </tr>
            ))}
          </tbody>
        </table>

        <section className="mt-6 flex justify-end">
          <dl className="w-64 space-y-1.5 text-sm">
            <div className="flex justify-between">
              <dt className="text-slate-500">Subtotal</dt>
              <dd className="text-slate-800">{currency(invoice.subTotal)}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-slate-500">GST ({percent(invoice.gstRate)} incl.)</dt>
              <dd className="text-slate-800">{currency(invoice.taxAmount)}</dd>
            </div>
            {invoice.discount > 0 && (
              <div className="flex justify-between">
                <dt className="text-slate-500">Discount</dt>
                <dd className="text-slate-800">-{currency(invoice.discount)}</dd>
              </div>
            )}
            {invoice.shippingFee > 0 && (
              <div className="flex justify-between">
                <dt className="text-slate-500">Shipping</dt>
                <dd className="text-slate-800">{currency(invoice.shippingFee)}</dd>
              </div>
            )}
            <div className="flex justify-between border-t border-slate-300 pt-1.5 text-base font-semibold text-slate-900">
              <dt>Total</dt>
              <dd>{currency(invoice.totalAmount)}</dd>
            </div>
          </dl>
        </section>

        {invoice.isPaid && (
          <section className="mt-8 rounded-md bg-green-50 p-4 text-sm text-green-800">
            <p className="font-semibold">Paid {date(invoice.datePaid)}</p>
            <p className="mt-1">
              {[
                invoice.modeOfPayment && `Mode: ${invoice.modeOfPayment}`,
                invoice.amountPaid != null && `Amount: ${currency(invoice.amountPaid)}`,
                invoice.cashAmount != null && `Cash: ${currency(invoice.cashAmount)}`,
                invoice.cardAmount != null && `Card: ${currency(invoice.cardAmount)}`,
              ]
                .filter(Boolean)
                .join(' · ')}
            </p>
          </section>
        )}

        {invoice.notes && (
          <section className="mt-8">
            <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Notes</p>
            <p className="mt-1 whitespace-pre-wrap text-sm text-slate-700">{invoice.notes}</p>
          </section>
        )}

        {business.bankDetails && (
          <section className="mt-8">
            <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Payment details</p>
            <p className="mt-1 whitespace-pre-line text-sm text-slate-700">{business.bankDetails}</p>
          </section>
        )}

        <footer className="mt-12 border-t border-slate-200 pt-4 text-center text-xs text-slate-400">
          Thank you for your business.
        </footer>
      </div>
    </div>
  )
}
