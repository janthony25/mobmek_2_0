import { useEffect, useState } from 'react'
import { checkCustomerDuplicates } from '@/api/customers'
import { Button } from '@/components/ui/Button'
import { Field, controlClass } from './controls'
import type { Customer, CustomerDuplicateMatch } from '@/types'

interface CustomerFormProps {
  initial: Customer | null
  onSubmit: (values: Record<string, unknown>) => Promise<void>
  onCancel: () => void
}

/** Bespoke customer form (rather than the generic schema form) so phone/email can run a
 * debounced, non-blocking duplicate check as the user types — see docs/features/customers-vehicles.md. */
export function CustomerForm({ initial, onSubmit, onCancel }: CustomerFormProps) {
  const [firstName, setFirstName] = useState(initial?.firstName ?? '')
  const [lastName, setLastName] = useState(initial?.lastName ?? '')
  const [phoneNumber, setPhoneNumber] = useState(initial?.phoneNumber ?? '')
  const [emailAddress, setEmailAddress] = useState(initial?.emailAddress ?? '')
  const [physicalAddress, setPhysicalAddress] = useState(initial?.physicalAddress ?? '')
  const [notes, setNotes] = useState(initial?.notes ?? '')

  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [duplicates, setDuplicates] = useState<CustomerDuplicateMatch[]>([])
  const [dismissedDuplicates, setDismissedDuplicates] = useState(false)

  // Advisory only — never blocks the save, just helps catch "didn't realize we already have
  // this customer" before it happens. Debounced so it doesn't hit the API on every keystroke.
  useEffect(() => {
    setDismissedDuplicates(false)
    const phone = phoneNumber.trim()
    const email = emailAddress.trim()
    if (!phone && !email) {
      setDuplicates([])
      return
    }
    const handle = setTimeout(() => {
      checkCustomerDuplicates(phone, email, initial?.id)
        .then(setDuplicates)
        .catch(() => setDuplicates([]))
    }, 400)
    return () => clearTimeout(handle)
  }, [phoneNumber, emailAddress, initial?.id])

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!firstName.trim() || !lastName.trim() || !phoneNumber.trim()) {
      setError('First name, last name and phone number are required.')
      return
    }
    setBusy(true)
    setError(null)
    try {
      await onSubmit({
        firstName: firstName.trim(),
        lastName: lastName.trim(),
        phoneNumber: phoneNumber.trim(),
        emailAddress: emailAddress.trim() || null,
        physicalAddress: physicalAddress.trim() || null,
        notes: notes.trim() || null,
      })
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Field label="First name" required>
          <input type="text" value={firstName} onChange={(e) => setFirstName(e.target.value)} className={controlClass} />
        </Field>
        <Field label="Last name" required>
          <input type="text" value={lastName} onChange={(e) => setLastName(e.target.value)} className={controlClass} />
        </Field>
        <Field label="Phone number" required>
          <input type="tel" value={phoneNumber} onChange={(e) => setPhoneNumber(e.target.value)} className={controlClass} />
        </Field>
        <Field label="Email">
          <input type="email" value={emailAddress} onChange={(e) => setEmailAddress(e.target.value)} className={controlClass} />
        </Field>
        <Field label="Address" className="sm:col-span-2">
          <input type="text" value={physicalAddress} onChange={(e) => setPhysicalAddress(e.target.value)} className={controlClass} />
        </Field>
        <Field label="Notes" className="sm:col-span-2">
          <textarea value={notes} onChange={(e) => setNotes(e.target.value)} rows={3} className={controlClass} />
        </Field>
      </div>

      {duplicates.length > 0 && !dismissedDuplicates && (
        <div className="rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-800">
          <div className="flex items-start justify-between gap-2">
            <p className="font-medium">Possible duplicate — this phone or email is already on file:</p>
            <button
              type="button"
              onClick={() => setDismissedDuplicates(true)}
              className="shrink-0 text-xs font-medium text-amber-700 hover:text-amber-900"
            >
              Dismiss
            </button>
          </div>
          <ul className="mt-1 list-disc pl-5">
            {duplicates.map((d) => (
              <li key={d.id}>
                {d.firstName} {d.lastName} — {d.phoneNumber}
                {d.emailAddress ? ` · ${d.emailAddress}` : ''}
              </li>
            ))}
          </ul>
          <p className="mt-1 text-xs text-amber-700">You can still save — this is just a heads-up.</p>
        </div>
      )}

      {error && <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>}

      <div className="flex justify-end gap-2 border-t border-slate-100 pt-4">
        <Button variant="secondary" onClick={onCancel} disabled={busy}>
          Cancel
        </Button>
        <Button type="submit" disabled={busy}>
          {busy ? 'Saving…' : 'Save'}
        </Button>
      </div>
    </form>
  )
}
