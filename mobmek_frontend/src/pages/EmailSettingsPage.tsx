import { useEffect, useState } from 'react'
import { getEmailSettings, sendTestEmail, updateEmailSettings } from '@/api/emailSettings'
import { ApiError } from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { StateMessage } from '@/components/ui/StateMessage'
import { useToast } from '@/components/ui/toast'
import { useAsync } from '@/hooks/useAsync'
import { date } from '@/lib/format'

const inputClass =
  'w-full rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500'

export function EmailSettingsPage() {
  const toast = useToast()
  const { data, loading, error, reload } = useAsync(getEmailSettings, [])

  const [fromName, setFromName] = useState('')
  const [fromAddress, setFromAddress] = useState('')
  const [replyToAddress, setReplyToAddress] = useState('')
  const [bccSelf, setBccSelf] = useState(true)
  const [saving, setSaving] = useState(false)

  const [testAddress, setTestAddress] = useState('')
  const [sendingTest, setSendingTest] = useState(false)

  useEffect(() => {
    if (!data) return
    setFromName(data.fromName)
    setFromAddress(data.fromAddress)
    setReplyToAddress(data.replyToAddress ?? '')
    setBccSelf(data.bccSelf)
  }, [data])

  if (loading && !data) return <StateMessage title="Loading email settings…" loading />
  if (error) return <StateMessage title="Could not load email settings" description={error.message} />

  const save = async () => {
    if (!fromName.trim() || !fromAddress.trim()) {
      toast.error('From name and from address are required.')
      return
    }
    setSaving(true)
    try {
      await updateEmailSettings({
        fromName: fromName.trim(),
        fromAddress: fromAddress.trim(),
        replyToAddress: replyToAddress.trim() || null,
        bccSelf,
      })
      toast.success('Email settings updated')
      reload()
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Failed to save settings.')
    } finally {
      setSaving(false)
    }
  }

  const sendTest = async () => {
    if (!testAddress.trim()) {
      toast.error('Enter an address to send the test to.')
      return
    }
    setSendingTest(true)
    try {
      const result = await sendTestEmail(testAddress.trim())
      // A 2xx response only means the attempt was recorded — the provider can still have
      // rejected it (bad from-address, bounce, etc), which lands as Status: Failed here rather
      // than as a thrown error.
      if (result.status === 'Failed' || result.status === 'Bounced') {
        toast.error(result.errorMessage ?? 'The email provider rejected this send.')
      } else {
        toast.success(`Test email sent to ${testAddress.trim()}`)
      }
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Failed to send test email.')
    } finally {
      setSendingTest(false)
    }
  }

  return (
    <section className="max-w-xl">
      <h1 className="text-2xl font-semibold text-slate-900">Email</h1>
      <p className="mt-1 text-sm text-slate-500">
        Controls the from-address and reply-to used when staff email invoices to customers.
      </p>

      <div className="mt-6 rounded-lg border border-slate-200 bg-white p-5">
        <div className="mb-4 flex items-center gap-2">
          <span className="text-sm font-medium text-slate-700">Resend API key</span>
          {data?.resendConfigured ? (
            <Badge tone="green">Configured</Badge>
          ) : (
            <Badge tone="amber">Not configured — set Email:Resend:ApiKey</Badge>
          )}
        </div>

        <div className="space-y-4">
          <label className="block">
            <span className="mb-1 block text-sm font-medium text-slate-700">From name</span>
            <input value={fromName} onChange={(e) => setFromName(e.target.value)} className={inputClass} />
          </label>
          <label className="block">
            <span className="mb-1 block text-sm font-medium text-slate-700">From address</span>
            <input
              type="email"
              value={fromAddress}
              onChange={(e) => setFromAddress(e.target.value)}
              className={inputClass}
            />
          </label>
          <label className="block">
            <span className="mb-1 block text-sm font-medium text-slate-700">Reply-to address</span>
            <input
              type="email"
              value={replyToAddress}
              onChange={(e) => setReplyToAddress(e.target.value)}
              placeholder="The workshop's real inbox, for customer replies"
              className={inputClass}
            />
          </label>
          <label className="flex items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" checked={bccSelf} onChange={(e) => setBccSelf(e.target.checked)} />
            BCC the reply-to address on every outbound email
          </label>
        </div>

        <div className="mt-4 flex items-center gap-4">
          <Button onClick={save} disabled={saving}>
            {saving ? 'Saving…' : 'Save'}
          </Button>
          {data?.updatedAtUtc && (
            <span className="text-xs text-slate-400">Last updated {date(data.updatedAtUtc)}</span>
          )}
        </div>
      </div>

      <div className="mt-6 rounded-lg border border-slate-200 bg-white p-5">
        <h2 className="text-sm font-medium text-slate-700">Send a test email</h2>
        <p className="mt-1 text-sm text-slate-500">Confirms the Resend configuration works end to end.</p>
        <div className="mt-3 flex gap-2">
          <input
            type="email"
            value={testAddress}
            onChange={(e) => setTestAddress(e.target.value)}
            placeholder="you@example.com"
            className={inputClass}
          />
          <Button type="button" variant="secondary" onClick={sendTest} disabled={sendingTest}>
            {sendingTest ? 'Sending…' : 'Send test'}
          </Button>
        </div>
      </div>
    </section>
  )
}
