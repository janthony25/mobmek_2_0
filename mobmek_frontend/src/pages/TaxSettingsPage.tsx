import { useEffect, useState } from 'react'
import { getGstSetting, updateGstSetting } from '@/api/gst'
import { Button } from '@/components/ui/Button'
import { StateMessage } from '@/components/ui/StateMessage'
import { useToast } from '@/components/ui/toast'
import { useAsync } from '@/hooks/useAsync'
import { date } from '@/lib/format'

export function TaxSettingsPage() {
  const toast = useToast()
  const { data, loading, error, reload } = useAsync(getGstSetting, [])
  // The API stores the rate as a fraction (0.15); the form edits it as a percentage (15).
  const [percentInput, setPercentInput] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (data) setPercentInput((data.rate * 100).toString())
  }, [data])

  if (loading) return <StateMessage title="Loading tax settings…" />
  if (error) return <StateMessage title="Could not load tax settings" description={error.message} />

  const save = async () => {
    const pct = Number(percentInput)
    if (Number.isNaN(pct) || pct < 0 || pct > 100) {
      toast.error('Enter a GST rate between 0 and 100.')
      return
    }
    setBusy(true)
    try {
      await updateGstSetting(pct / 100)
      toast.success('GST rate updated')
      reload()
    } catch (err) {
      toast.error(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="max-w-xl">
      <h1 className="text-2xl font-semibold text-slate-900">Tax (GST)</h1>
      <p className="mt-1 text-sm text-slate-500">
        GST is treated as inclusive — already part of the prices and shown on invoices for display,
        not added on top. New invoices snapshot this rate; existing invoices are unaffected.
      </p>

      <div className="mt-6 rounded-lg border border-slate-200 bg-white p-5">
        <label className="block">
          <span className="mb-1 block text-sm font-medium text-slate-700">GST rate (%)</span>
          <div className="flex items-center gap-2">
            <input
              type="number"
              step="0.01"
              min={0}
              max={100}
              value={percentInput}
              onChange={(e) => setPercentInput(e.target.value)}
              className="w-32 rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
            />
            <span className="text-sm text-slate-500">%</span>
          </div>
        </label>

        <div className="mt-4 flex items-center gap-4">
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
