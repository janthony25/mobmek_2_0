import { useEffect, useState } from 'react'
import { getEmailSettings, sendTestEmail, updateEmailSettings } from '@/api/emailSettings'
import { getEmailTemplates, previewEmailTemplate, updateEmailTemplate } from '@/api/emailTemplates'
import { ApiError } from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { StateMessage } from '@/components/ui/StateMessage'
import { UpdatedByTag } from '@/components/ui/UpdatedByTag'
import { useToast } from '@/components/ui/toast'
import { useAsync } from '@/hooks/useAsync'
import type { EmailTemplate } from '@/types'

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
            <Badge tone="amber">
              Not fully set up — needs a Resend API key (Email:Resend:ApiKey) and a From address below
            </Badge>
          )}
        </div>

        <div className="space-y-4">
          <label className="block">
            <span className="mb-1 block text-sm font-medium text-slate-700">From name</span>
            <input value={fromName} onChange={(e) => setFromName(e.target.value.toUpperCase())} className={inputClass} />
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
          <UpdatedByTag updatedAtUtc={data?.updatedAtUtc ?? null} updatedByName={data?.updatedByName ?? null} />
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

      <div className="mt-6 rounded-lg border border-slate-200 bg-white p-5">
        <h2 className="text-sm font-medium text-slate-700">Email wording</h2>
        <p className="mt-1 text-sm text-slate-500">
          The subject and intro paragraph used for each kind of outbound email. Use{' '}
          <code className="rounded bg-slate-100 px-1">{'{{Token}}'}</code> placeholders — an unknown token renders
          blank rather than erroring.
        </p>
        <EmailTemplatesEditor />
      </div>
    </section>
  )
}

function EmailTemplatesEditor() {
  const { data, loading, error, reload } = useAsync(getEmailTemplates, [])

  if (loading && !data) return <p className="mt-4 text-sm text-slate-400">Loading templates…</p>
  if (error) return <p className="mt-4 text-sm text-red-600">Could not load email templates.</p>

  return (
    <div className="mt-4 space-y-4 divide-y divide-slate-100">
      {data?.map((template) => <EmailTemplateRow key={template.key} template={template} onSaved={reload} />)}
    </div>
  )
}

function EmailTemplateRow({ template, onSaved }: { template: EmailTemplate; onSaved: () => void }) {
  const toast = useToast()
  const [subject, setSubject] = useState(template.subjectTemplate)
  const [intro, setIntro] = useState(template.bodyIntroTemplate)
  const [saving, setSaving] = useState(false)
  const [previewing, setPreviewing] = useState(false)
  const [preview, setPreview] = useState<{ subject: string; bodyIntro: string } | null>(null)

  const dirty = subject !== template.subjectTemplate || intro !== template.bodyIntroTemplate

  const save = async () => {
    setSaving(true)
    try {
      await updateEmailTemplate(template.key, { subjectTemplate: subject, bodyIntroTemplate: intro })
      toast.success(`${template.name} updated`)
      onSaved()
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Failed to save template.')
    } finally {
      setSaving(false)
    }
  }

  const showPreview = async () => {
    setPreviewing(true)
    try {
      setPreview(await previewEmailTemplate(template.key))
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Failed to render preview.')
    } finally {
      setPreviewing(false)
    }
  }

  return (
    <div className="pt-4 first:pt-0">
      <h3 className="text-sm font-semibold text-slate-800">{template.name}</h3>
      <div className="mt-2 space-y-3">
        <label className="block">
          <span className="mb-1 block text-xs font-medium text-slate-600">Subject</span>
          <input value={subject} onChange={(e) => setSubject(e.target.value)} className={inputClass} />
        </label>
        <label className="block">
          <span className="mb-1 block text-xs font-medium text-slate-600">Intro paragraph</span>
          <textarea rows={2} value={intro} onChange={(e) => setIntro(e.target.value)} className={inputClass} />
        </label>
      </div>

      {preview && (
        <div className="mt-2 rounded-md bg-slate-50 p-3 text-xs text-slate-600">
          <p className="font-medium text-slate-700">{preview.subject}</p>
          <p className="mt-1">{preview.bodyIntro}</p>
        </div>
      )}

      <div className="mt-2 flex items-center gap-2">
        <Button type="button" size="sm" onClick={save} disabled={saving || !dirty}>
          {saving ? 'Saving…' : 'Save'}
        </Button>
        <Button type="button" size="sm" variant="secondary" onClick={showPreview} disabled={previewing}>
          {previewing ? 'Rendering…' : 'Preview'}
        </Button>
      </div>
    </div>
  )
}
