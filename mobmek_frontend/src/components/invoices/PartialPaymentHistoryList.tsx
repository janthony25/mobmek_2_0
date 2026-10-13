import { useState } from 'react'
import { deletePartialPayment, setPartialPaymentVisibility, updatePartialPaymentNotes } from '@/api/invoices'
import { Button } from '@/components/ui/Button'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { useToast } from '@/components/ui/toast'
import { currency, date } from '@/lib/format'
import { controlClass } from '@/components/forms/controls'
import type { InvoicePartialPayment } from '@/types'

export interface PartialPaymentHistoryEntry {
  payment: InvoicePartialPayment
  jobId: string
  invoiceId: string
  /** Shown alongside the row when aggregating payments across more than one invoice (e.g. the job form). */
  invoiceLabel?: string
}

/**
 * Read-only list of recorded partial payments — each row can only be changed via its own
 * "Edit note" (text only; amount/date/mode are locked) or "Delete" (confirmation-gated) button,
 * never by hand-editing a shared text field. Used both inside the Partial Payment wizard and
 * above the job form's Invoice Notes field.
 */
export function PartialPaymentHistoryList({
  title = 'Payment history',
  entries,
  onChanged,
}: {
  title?: string
  entries: PartialPaymentHistoryEntry[]
  onChanged: () => void | Promise<void>
}) {
  const toast = useToast()
  const [editing, setEditing] = useState<PartialPaymentHistoryEntry | null>(null)
  const [editNotes, setEditNotes] = useState('')
  const [deleting, setDeleting] = useState<PartialPaymentHistoryEntry | null>(null)
  const [busy, setBusy] = useState(false)
  const [togglingId, setTogglingId] = useState<string | null>(null)

  if (entries.length === 0) {
    return null
  }

  const toggleShowOnInvoice = async (entry: PartialPaymentHistoryEntry, showOnInvoice: boolean) => {
    setTogglingId(entry.payment.id)
    try {
      await setPartialPaymentVisibility(entry.jobId, entry.invoiceId, entry.payment.id, { showOnInvoice })
      await onChanged()
    } catch (err) {
      toast.error(err instanceof Error ? err.message : String(err))
    } finally {
      setTogglingId(null)
    }
  }

  const saveNotes = async () => {
    if (!editing) return
    setBusy(true)
    try {
      await updatePartialPaymentNotes(editing.jobId, editing.invoiceId, editing.payment.id, { notes: editNotes.trim() || null })
      await onChanged()
      setEditing(null)
      toast.success('Note updated')
    } catch (err) {
      toast.error(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  const confirmDelete = async () => {
    if (!deleting) return
    await deletePartialPayment(deleting.jobId, deleting.invoiceId, deleting.payment.id)
    await onChanged()
    setDeleting(null)
    toast.success('Partial payment deleted')
  }

  return (
    <div>
      <p className="mb-2 text-sm font-medium text-slate-700">{title}</p>
      <ul className="divide-y divide-slate-100 rounded-md border border-slate-200">
        {entries.map(({ payment: p, ...entry }) => (
          <li key={p.id} className="flex items-start justify-between gap-3 px-3 py-2">
            {editing?.payment.id === p.id ? (
              <div className="flex-1 space-y-2">
                <textarea
                  value={editNotes}
                  onChange={(e) => setEditNotes(e.target.value)}
                  rows={2}
                  className={controlClass}
                  autoFocus
                />
                <div className="flex justify-end gap-2">
                  <Button type="button" variant="secondary" onClick={() => setEditing(null)} disabled={busy}>
                    Cancel
                  </Button>
                  <Button type="button" onClick={saveNotes} disabled={busy}>
                    Save note
                  </Button>
                </div>
              </div>
            ) : (
              <>
                <div className="min-w-0">
                  <p className="text-sm text-slate-900">
                    {date(p.paymentDate)} — {currency(p.amount)} via {p.modeOfPayment ?? 'payment'}
                    {entry.invoiceLabel && <span className="text-slate-500"> ({entry.invoiceLabel})</span>}
                  </p>
                  {p.notes && <p className="text-sm text-slate-500">{p.notes}</p>}
                </div>
                <div className="flex shrink-0 items-center gap-3">
                  <label className="flex items-center gap-1.5 text-xs text-slate-500">
                    <input
                      type="checkbox"
                      checked={p.showOnInvoice}
                      disabled={togglingId === p.id}
                      onChange={(e) => toggleShowOnInvoice({ payment: p, ...entry }, e.target.checked)}
                      className="h-3.5 w-3.5 rounded border-slate-300"
                    />
                    Show on invoice
                  </label>
                  <button
                    type="button"
                    className="text-xs text-slate-500 hover:underline"
                    onClick={() => {
                      setEditing({ payment: p, ...entry })
                      setEditNotes(p.notes ?? '')
                    }}
                  >
                    Edit note
                  </button>
                  <button
                    type="button"
                    className="text-xs text-red-600 hover:underline"
                    onClick={() => setDeleting({ payment: p, ...entry })}
                  >
                    Delete
                  </button>
                </div>
              </>
            )}
          </li>
        ))}
      </ul>

      <ConfirmDialog
        open={deleting !== null}
        title="Delete partial payment"
        message={
          deleting
            ? `Delete the ${currency(deleting.payment.amount)} payment recorded on ${date(deleting.payment.paymentDate)}? This cannot be undone.`
            : ''
        }
        confirmLabel="Delete"
        onConfirm={confirmDelete}
        onCancel={() => setDeleting(null)}
      />
    </div>
  )
}
