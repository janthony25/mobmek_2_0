import { useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { forgotPassword, resetForgottenPassword } from '@/api/auth'
import { ApiError } from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Field, controlClass } from '@/components/forms/controls'

/** Public — for a user who's locked out and can't sign in to reach the authenticated
 * Profile → Change password flow. Deliberately generic messaging throughout: the backend
 * never reveals whether an email actually has an account. */
export function ForgotPasswordPage() {
  const navigate = useNavigate()
  const [stage, setStage] = useState<'request' | 'reset'>('request')
  const [email, setEmail] = useState('')
  const [code, setCode] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState(false)
  const [submitting, setSubmitting] = useState(false)

  const sendCode = async () => {
    setError(null)
    setSubmitting(true)
    try {
      await forgotPassword({ email })
      setStage('reset')
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Something went wrong. Please try again.')
    } finally {
      setSubmitting(false)
    }
  }

  const requestCode = (event: FormEvent) => {
    event.preventDefault()
    void sendCode()
  }

  const resetPassword = async (event: FormEvent) => {
    event.preventDefault()
    setError(null)
    if (newPassword !== confirmPassword) {
      setError('Passwords do not match.')
      return
    }
    setSubmitting(true)
    try {
      await resetForgottenPassword({ email, code, newPassword })
      setDone(true)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not reset your password. Please try again.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-4">
      <div className="w-full max-w-sm rounded-lg border border-slate-200 bg-white p-8 shadow-sm">
        <div className="mb-6 text-center">
          <h1 className="mt-2 text-lg font-semibold text-slate-900">Reset your password</h1>
        </div>

        {done ? (
          <div className="space-y-4 text-center">
            <p className="text-sm text-slate-700">Your password has been reset. You can now sign in.</p>
            <Button className="w-full justify-center" onClick={() => navigate('/login', { replace: true })}>
              Go to sign in
            </Button>
          </div>
        ) : stage === 'request' ? (
          <form onSubmit={requestCode} className="space-y-4">
            <p className="text-center text-sm text-slate-500">
              Enter your email and we'll send a 6-digit code if an account exists for it.
            </p>
            <Field label="Email" required>
              <input
                type="email"
                required
                autoComplete="email"
                autoFocus
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                className={controlClass}
              />
            </Field>

            {error && <p className="text-sm text-red-600">{error}</p>}

            <Button type="submit" disabled={submitting} className="w-full justify-center">
              {submitting ? 'Sending…' : 'Send code'}
            </Button>
            <button
              type="button"
              onClick={() => navigate('/login')}
              className="w-full text-center text-xs font-medium text-slate-500 hover:text-slate-800"
            >
              Back to sign in
            </button>
          </form>
        ) : (
          <form onSubmit={resetPassword} className="space-y-4">
            <p className="text-center text-sm text-slate-500">
              If an account exists for <span className="font-medium text-slate-700">{email}</span>, a code was just
              emailed to it.
            </p>
            <Field label="6-digit code" required>
              <input
                required
                maxLength={6}
                autoComplete="one-time-code"
                autoFocus
                value={code}
                onChange={(event) => setCode(event.target.value)}
                className={controlClass}
              />
            </Field>
            <Field label="New password" required>
              <input
                type="password"
                required
                autoComplete="new-password"
                value={newPassword}
                onChange={(event) => setNewPassword(event.target.value)}
                className={controlClass}
              />
            </Field>
            <Field label="Confirm new password" required>
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
              {submitting ? 'Resetting…' : 'Reset password'}
            </Button>
            <div className="flex items-center justify-center gap-4">
              <button
                type="button"
                onClick={() => void sendCode()}
                disabled={submitting}
                className="text-xs font-medium text-slate-500 hover:text-slate-800"
              >
                Resend code
              </button>
              <button
                type="button"
                onClick={() => setStage('request')}
                className="text-xs font-medium text-slate-500 hover:text-slate-800"
              >
                Use a different email
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  )
}
