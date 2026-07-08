import { useState } from 'react'
import type { FormEvent } from 'react'
import { sendInvoiceEmail } from '@/api/invoices'
import { ApiError } from '@/api/client'
import { Button } from '@/components/ui/Button'
import { useToast } from '@/components/ui/toast'
import { Field, controlClass } from '@/components/forms/controls'
import { currency, percent } from '@/lib/format'
import type { Invoice } from '@/types'
import { EmailHistoryList } from './EmailHistoryList'

interface EmailComposeModalProps {
  jobId: string
  invoice: Invoice
  onSent: () => void
  onClose: () => void
}

/** Compose form + send history for one invoice. Rendered as the content of an outer
 * `<Modal>` (see InvoicesSection), not a modal itself — same shape as GenerateForm/MarkPaidForm. */
export function EmailComposeModal({ jobId, invoice, onSent, onClose }: EmailComposeModalProps) {
  const toast = useToast()
  const [to, setTo] = useState(invoice.customerEmail ?? '')
  const [toName, setToName] = useState(invoice.customerName ?? '')
  const [cc, setCc] = useState('')
  const [subject, setSubject] = useState(`${invoice.documentType} ${invoice.invoiceNumber}`)
  const [intro, setIntro] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [historyReloadKey, setHistoryReloadKey] = useState(0)

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const result = await sendInvoiceEmail(jobId, invoice.id, {
        to,
        toName: toName || null,
        cc: cc || null,
        subject,
        intro: intro || null,
      })
      setHistoryReloadKey((k) => k + 1)
      onSent()
      // A 2xx response only means the send attempt was recorded — the provider can still have
      // rejected it (bad from-address, bounce, etc), which lands as Status: Failed/Bounced here
      // rather than as a thrown error. Surface that distinctly from an actual success.
      if (result.status === 'Failed' || result.status === 'Bounced') {
        toast.error(result.errorMessage ?? 'The email provider rejected this send.')
      } else {
        toast.success(`Email sent to ${result.toAddress}`)
      }
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to send email. Please try again.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="space-y-5">
      <form onSubmit={submit} className="space-y-4">
        {!invoice.customerEmail && (
          <p className="rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-700">
            This customer has no email on file — enter one below to send this once.
          </p>
        )}
        <div className="grid grid-cols-2 gap-3">
          <Field label="To" required>
            <input
              type="email"
              required
              value={to}
              onChange={(e) => setTo(e.target.value)}
              className={controlClass}
            />
          </Field>
          <Field label="Recipient name">
            <input
              type="text"
              value={toName}
              onChange={(e) => setToName(e.target.value)}
              className={controlClass}
            />
          </Field>
        </div>
        <Field label="CC">
          <input type="email" value={cc} onChange={(e) => setCc(e.target.value)} className={controlClass} />
        </Field>
        <Field label="Subject" required>
          <input
            type="text"
            required
            value={subject}
            onChange={(e) => setSubject(e.target.value)}
            className={controlClass}
          />
        </Field>
        <Field label="Message">
          <textarea
            rows={3}
            value={intro}
            onChange={(e) => setIntro(e.target.value)}
            placeholder="Optional note shown above the invoice details"
            className={controlClass}
          />
        </Field>

        <div className="rounded-lg border border-slate-200 bg-slate-50 p-3 text-sm">
          <p className="mb-2 font-medium text-slate-700">{invoice.documentType} {invoice.invoiceNumber}</p>
          <ul className="space-y-1 text-slate-600">
            {invoice.items.map((item) => (
              <li key={item.id} className="flex justify-between">
                <span>
                  {item.itemName} × {item.quantity}
                </span>
                <span>{currency(item.itemTotal)}</span>
              </li>
            ))}
          </ul>
          <div className="mt-2 flex justify-between border-t border-slate-200 pt-2 text-slate-600">
            <span>GST ({percent(invoice.gstRate)})</span>
            <span>{currency(invoice.taxAmount)}</span>
          </div>
          <div className="flex justify-between font-semibold text-slate-900">
            <span>Total</span>
            <span>{currency(invoice.totalAmount)}</span>
          </div>
        </div>

        {error && <p className="text-sm text-red-600">{error}</p>}

        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose} disabled={busy}>
            Close
          </Button>
          <Button type="submit" disabled={busy}>
            {busy ? 'Sending…' : 'Send'}
          </Button>
        </div>
      </form>

      <div className="border-t border-slate-100 pt-4">
        <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-500">History</h3>
        <EmailHistoryList invoiceId={invoice.id} reloadKey={historyReloadKey} />
      </div>
    </div>
  )
}
