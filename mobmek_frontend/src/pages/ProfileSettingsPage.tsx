import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { confirmPasswordChange, getProfile, requestPasswordChangeCode, updateProfile } from '@/api/account'
import { ApiError } from '@/api/client'
import { Button } from '@/components/ui/Button'
import { StateMessage } from '@/components/ui/StateMessage'
import { useToast } from '@/components/ui/toast'
import { useAsync } from '@/hooks/useAsync'

const inputClass =
  'w-full rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500'

export function ProfileSettingsPage() {
  const toast = useToast()
  const { data, loading, error, reload } = useAsync(getProfile, [])

  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [contactNumber, setContactNumber] = useState('')
  const [physicalAddress, setPhysicalAddress] = useState('')
  const [saving, setSaving] = useState(false)

  const [codeRequested, setCodeRequested] = useState(false)
  const [requestingCode, setRequestingCode] = useState(false)
  const [code, setCode] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmNewPassword, setConfirmNewPassword] = useState('')
  const [confirming, setConfirming] = useState(false)

  useEffect(() => {
    if (!data) return
    setFirstName(data.firstName)
    setLastName(data.lastName)
    setContactNumber(data.contactNumber)
    setPhysicalAddress(data.physicalAddress)
  }, [data])

  if (loading && !data) return <StateMessage title="Loading your profile…" loading />
  if (error) return <StateMessage title="Could not load your profile" description={error.message} />

  const saveDetails = async () => {
    if (!firstName.trim() || !lastName.trim() || !contactNumber.trim() || !physicalAddress.trim()) {
      toast.error('All fields are required.')
      return
    }
    setSaving(true)
    try {
      await updateProfile({
        firstName: firstName.trim(),
        lastName: lastName.trim(),
        contactNumber: contactNumber.trim(),
        physicalAddress: physicalAddress.trim(),
      })
      toast.success('Profile updated')
      reload()
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Failed to save profile.')
    } finally {
      setSaving(false)
    }
  }

  const sendCode = async () => {
    setRequestingCode(true)
    try {
      await requestPasswordChangeCode()
      toast.success('Code sent — check your email.')
      setCodeRequested(true)
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Failed to send the code.')
    } finally {
      setRequestingCode(false)
    }
  }

  const confirmChange = async (event: FormEvent) => {
    event.preventDefault()
    if (newPassword !== confirmNewPassword) {
      toast.error('Passwords do not match.')
      return
    }
    setConfirming(true)
    try {
      await confirmPasswordChange({ code, newPassword })
      toast.success('Password changed')
      setCodeRequested(false)
      setCode('')
      setNewPassword('')
      setConfirmNewPassword('')
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Failed to change password.')
    } finally {
      setConfirming(false)
    }
  }

  return (
    <section className="max-w-xl">
      <h1 className="text-2xl font-semibold text-slate-900">Profile</h1>
      <p className="mt-1 text-sm text-slate-500">Your own name, contact details, and password.</p>

      <div className="mt-6 rounded-lg border border-slate-200 bg-white p-5">
        <h2 className="text-sm font-medium text-slate-700">Your details</h2>
        <div className="mt-3 space-y-4">
          <label className="block">
            <span className="mb-1 block text-sm font-medium text-slate-700">Email</span>
            <input value={data?.email ?? ''} disabled className={`${inputClass} bg-slate-50 text-slate-500`} />
            <span className="mt-1 block text-xs text-slate-400">Only an admin can change your login email.</span>
          </label>
          <div className="grid grid-cols-2 gap-3">
            <label className="block">
              <span className="mb-1 block text-sm font-medium text-slate-700">First name</span>
              <input value={firstName} onChange={(e) => setFirstName(e.target.value.toUpperCase())} className={inputClass} />
            </label>
            <label className="block">
              <span className="mb-1 block text-sm font-medium text-slate-700">Last name</span>
              <input value={lastName} onChange={(e) => setLastName(e.target.value.toUpperCase())} className={inputClass} />
            </label>
          </div>
          <label className="block">
            <span className="mb-1 block text-sm font-medium text-slate-700">Contact number</span>
            <input value={contactNumber} onChange={(e) => setContactNumber(e.target.value.toUpperCase())} className={inputClass} />
          </label>
          <label className="block">
            <span className="mb-1 block text-sm font-medium text-slate-700">Address</span>
            <input value={physicalAddress} onChange={(e) => setPhysicalAddress(e.target.value.toUpperCase())} className={inputClass} />
          </label>
        </div>

        <div className="mt-4">
          <Button onClick={saveDetails} disabled={saving}>
            {saving ? 'Saving…' : 'Save'}
          </Button>
        </div>
      </div>

      <div className="mt-6 rounded-lg border border-slate-200 bg-white p-5">
        <h2 className="text-sm font-medium text-slate-700">Change password</h2>
        <p className="mt-1 text-sm text-slate-500">
          We'll email a 6-digit code to confirm it's you — no need to enter your current password.
        </p>

        {!codeRequested ? (
          <div className="mt-3">
            <Button type="button" variant="secondary" onClick={sendCode} disabled={requestingCode}>
              {requestingCode ? 'Sending…' : 'Send code to my email'}
            </Button>
          </div>
        ) : (
          <form onSubmit={confirmChange} className="mt-3 space-y-4">
            <label className="block">
              <span className="mb-1 block text-sm font-medium text-slate-700">6-digit code</span>
              <input
                value={code}
                onChange={(e) => setCode(e.target.value)}
                maxLength={6}
                required
                autoComplete="one-time-code"
                className={inputClass}
              />
            </label>
            <label className="block">
              <span className="mb-1 block text-sm font-medium text-slate-700">New password</span>
              <input
                type="password"
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                required
                autoComplete="new-password"
                className={inputClass}
              />
            </label>
            <label className="block">
              <span className="mb-1 block text-sm font-medium text-slate-700">Confirm new password</span>
              <input
                type="password"
                value={confirmNewPassword}
                onChange={(e) => setConfirmNewPassword(e.target.value)}
                required
                autoComplete="new-password"
                className={inputClass}
              />
            </label>
            <div className="flex items-center gap-3">
              <Button type="submit" disabled={confirming}>
                {confirming ? 'Changing…' : 'Change password'}
              </Button>
              <button
                type="button"
                onClick={sendCode}
                disabled={requestingCode}
                className="text-xs font-medium text-slate-500 hover:text-slate-800"
              >
                Resend code
              </button>
              <button
                type="button"
                onClick={() => {
                  setCodeRequested(false)
                  setCode('')
                  setNewPassword('')
                  setConfirmNewPassword('')
                }}
                className="text-xs font-medium text-slate-500 hover:text-slate-800"
              >
                Cancel
              </button>
            </div>
          </form>
        )}
      </div>
    </section>
  )
}
