import { useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { confirmAccount, getInvitePreview } from '@/api/accounts'
import { ApiError } from '@/api/client'
import { Button } from '@/components/ui/Button'
import { StateMessage } from '@/components/ui/StateMessage'
import { Field, controlClass } from '@/components/forms/controls'
import { useAsync } from '@/hooks/useAsync'

/** Public — reached from the activation link emailed when an Admin creates an account. */
export function ConfirmAccountPage() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const token = searchParams.get('token') ?? ''

  const preview = useAsync(() => (token ? getInvitePreview(token) : Promise.reject(new Error('Missing token'))), [token])

  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState(false)
  const [submitting, setSubmitting] = useState(false)

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    setError(null)
    if (newPassword !== confirmPassword) {
      setError('Passwords do not match.')
      return
    }
    setSubmitting(true)
    try {
      await confirmAccount({ token, newPassword })
      setDone(true)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not activate the account. Please try again.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-4">
      <div className="w-full max-w-sm rounded-lg border border-slate-200 bg-white p-8 shadow-sm">
        <div className="mb-6 text-center">
          <h1 className="mt-2 text-lg font-semibold text-slate-900">Activate your account</h1>
        </div>

        {!token ? (
          <StateMessage title="Invalid link" description="This activation link is missing its token." />
        ) : preview.loading ? (
          <StateMessage title="Checking your invite…" loading />
        ) : preview.error || !preview.data ? (
          <StateMessage
            title="This link is invalid or has expired"
            description="Ask an Admin to create a new account for you to get a fresh link."
          />
        ) : done ? (
          <div className="space-y-4 text-center">
            <p className="text-sm text-slate-700">Your account is active. You can now sign in.</p>
            <Button className="w-full justify-center" onClick={() => navigate('/login', { replace: true })}>
              Go to sign in
            </Button>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-4">
            <p className="text-center text-sm text-slate-500">
              Setting a password for <span className="font-medium text-slate-700">{preview.data.firstName} {preview.data.lastName}</span> (
              {preview.data.email})
            </p>
            <Field label="New password" required>
              <input
                type="password"
                required
                autoComplete="new-password"
                autoFocus
                value={newPassword}
                onChange={(event) => setNewPassword(event.target.value)}
                className={controlClass}
              />
            </Field>
            <Field label="Confirm password" required>
              <input
                type="password"
                required
                autoComplete="new-password"
                value={confirmPassword}
                onChange={(event) => setConfirmPassword(event.target.value)}
                className={controlClass}
              />
            </Field>

            {error && <p className="text-sm text-red-600">{error}</p>}

            <Button type="submit" disabled={submitting} className="w-full justify-center">
              {submitting ? 'Activating…' : 'Activate account'}
            </Button>
          </form>
        )}
      </div>
    </div>
  )
}
