import { useState } from 'react'
import type { FormEvent } from 'react'
import { sendAppointmentEmail } from '@/api/appointments'
import { ApiError } from '@/api/client'
import { Button } from '@/components/ui/Button'
import { useToast } from '@/components/ui/toast'
import { Field, controlClass } from '@/components/forms/controls'
import { date, time } from '@/lib/format'
import type { Appointment } from '@/types'

interface AppointmentEmailModalProps {
  appointment: Appointment
  onSent: () => void
  onClose: () => void
}

/** Compose form for emailing an appointment confirmation. Rendered as the content of an
 * outer `<Modal>` (see AppointmentDetailModal), same shape as the invoice EmailComposeModal.
 * Defaults to the linked customer's email, falling back to the phone-call contact email for
 * an appointment not yet converted. */
export function AppointmentEmailModal({ appointment, onSent, onClose }: AppointmentEmailModalProps) {
  const toast = useToast()
  const [to, setTo] = useState(appointment.customerEmail ?? appointment.contactEmail ?? '')
  const [toName, setToName] = useState(appointment.customerName ?? appointment.contactName ?? '')
  const [cc, setCc] = useState('')
  const [subject, setSubject] = useState(`Your appointment — ${date(appointment.startUtc)}`)
  const [intro, setIntro] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const result = await sendAppointmentEmail(appointment.id, {
        to,
        toName: toName || null,
        cc: cc || null,
        subject,
        intro: intro || null,
      })
      onSent()
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

  const hasDefaultRecipient = Boolean(appointment.customerEmail ?? appointment.contactEmail)

  return (
    <form onSubmit={submit} className="space-y-4">
      {!hasDefaultRecipient && (
        <p className="rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-700">
          No email on file for this appointment — enter one below to send this once.
        </p>
      )}
      <div className="grid grid-cols-2 gap-3">
        <Field label="To" required>
          <input type="email" required value={to} onChange={(e) => setTo(e.target.value)} className={controlClass} />
        </Field>
        <Field label="Recipient name">
          <input type="text" value={toName} onChange={(e) => setToName(e.target.value)} className={controlClass} />
        </Field>
      </div>
      <Field label="CC">
        <input type="email" value={cc} onChange={(e) => setCc(e.target.value)} className={controlClass} />
      </Field>
      <Field label="Subject" required>
        <input type="text" required value={subject} onChange={(e) => setSubject(e.target.value)} className={controlClass} />
      </Field>
      <Field label="Message">
        <textarea
          rows={3}
          value={intro}
          onChange={(e) => setIntro(e.target.value)}
          placeholder="Optional note — leave blank to use the standard confirmation wording"
          className={controlClass}
        />
      </Field>

      <div className="rounded-lg border border-slate-200 bg-slate-50 p-3 text-sm text-slate-600">
        <p className="font-medium text-slate-700">{appointment.title}</p>
        <p>
          {date(appointment.startUtc)} · {time(appointment.startUtc)} – {time(appointment.endUtc)}
        </p>
        {(appointment.carDescription ?? appointment.vehicleDescription) && (
          <p>{appointment.carDescription ?? appointment.vehicleDescription}</p>
        )}
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
  )
}
