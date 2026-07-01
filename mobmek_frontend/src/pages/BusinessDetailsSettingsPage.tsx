import { useEffect, useState } from 'react'
import { getBusinessDetails, updateBusinessDetails } from '@/api/businessDetails'
import { Button } from '@/components/ui/Button'
import { StateMessage } from '@/components/ui/StateMessage'
import { useToast } from '@/components/ui/toast'
import { useAsync } from '@/hooks/useAsync'
import { date } from '@/lib/format'

const inputClass =
  'w-full rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500'

export function BusinessDetailsSettingsPage() {
  const toast = useToast()
  const { data, loading, error, reload } = useAsync(getBusinessDetails, [])

  const [name, setName] = useState('')
  const [address, setAddress] = useState('')
  const [phone, setPhone] = useState('')
  const [email, setEmail] = useState('')
  const [abn, setAbn] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (!data) return
    setName(data.name)
    setAddress(data.address ?? '')
    setPhone(data.phone ?? '')
    setEmail(data.email ?? '')
    setAbn(data.abn ?? '')
  }, [data])

  if (loading) return <StateMessage title="Loading business details…" />
  if (error) return <StateMessage title="Could not load business details" description={error.message} />

  const save = async () => {
    if (!name.trim()) {
      toast.error('Business name is required.')
      return
    }
    setBusy(true)
    try {
      await updateBusinessDetails({
        name: name.trim(),
        address: address.trim() || null,
        phone: phone.trim() || null,
        email: email.trim() || null,
        abn: abn.trim() || null,
      })
      toast.success('Business details updated')
      reload()
    } catch (err) {
      toast.error(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="max-w-xl">
      <h1 className="text-2xl font-semibold text-slate-900">Business Details</h1>
      <p className="mt-1 text-sm text-slate-500">
        Shown as the letterhead on invoice PDFs.
      </p>

      <div className="mt-6 space-y-4 rounded-lg border border-slate-200 bg-white p-5">
        <label className="block">
          <span className="mb-1 block text-sm font-medium text-slate-700">
            Business name<span className="text-red-500"> *</span>
          </span>
          <input type="text" value={name} onChange={(e) => setName(e.target.value)} className={inputClass} />
        </label>
        <label className="block">
          <span className="mb-1 block text-sm font-medium text-slate-700">Address</span>
          <textarea value={address} rows={2} onChange={(e) => setAddress(e.target.value)} className={inputClass} />
        </label>
        <div className="grid grid-cols-2 gap-4">
          <label className="block">
            <span className="mb-1 block text-sm font-medium text-slate-700">Phone</span>
            <input type="text" value={phone} onChange={(e) => setPhone(e.target.value)} className={inputClass} />
          </label>
          <label className="block">
            <span className="mb-1 block text-sm font-medium text-slate-700">Email</span>
            <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} className={inputClass} />
          </label>
        </div>
        <label className="block">
          <span className="mb-1 block text-sm font-medium text-slate-700">ABN</span>
          <input type="text" value={abn} onChange={(e) => setAbn(e.target.value)} className={inputClass} />
        </label>

        <div className="flex items-center gap-4 border-t border-slate-100 pt-4">
          <Button onClick={save} disabled={busy}>
            Save
          </Button>
          {data?.updatedAtUtc && (
            <span className="text-xs text-slate-400">Last updated {date(data.updatedAtUtc)}</span>
          )}
        </div>
      </div>
    </section>
  )
}
