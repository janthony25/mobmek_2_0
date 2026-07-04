import { useState } from 'react'
import { getGstReport } from '@/api/gst'
import { Card } from '@/components/ui/Card'
import { PageHeader } from '@/components/ui/PageHeader'
import { StateMessage } from '@/components/ui/StateMessage'
import { Field, controlClass } from '@/components/forms/controls'
import { useAsync } from '@/hooks/useAsync'
import { currency } from '@/lib/format'
import type { GstScopeTotals } from '@/types'

// Defaults to the current calendar month. Formats in local time (not toISOString, which is
// UTC and rolls the date back a day in negative-UTC-offset timezones).
function currentMonthRange(): { from: string; to: string } {
  const now = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  const iso = (y: number, m: number, d: number) => `${y}-${pad(m + 1)}-${pad(d)}`
  const lastDay = new Date(now.getFullYear(), now.getMonth() + 1, 0).getDate()
  return { from: iso(now.getFullYear(), now.getMonth(), 1), to: iso(now.getFullYear(), now.getMonth(), lastDay) }
}

function ScopeCard({ title, description, totals }: { title: string; description: string; totals: GstScopeTotals }) {
  return (
    <Card title={title}>
      <p className="mb-4 text-sm text-slate-500">{description}</p>
      <dl className="space-y-2 text-sm">
        <div className="flex items-center justify-between">
          <dt className="text-slate-600">GST on sales</dt>
          <dd className="font-medium text-slate-900">{currency(totals.gstOnSales)}</dd>
        </div>
        <div className="flex items-center justify-between">
          <dt className="text-slate-600">GST on purchases</dt>
          <dd className="font-medium text-slate-900">{currency(totals.gstOnPurchases)}</dd>
        </div>
        <div className="flex items-center justify-between border-t border-slate-200 pt-2">
          <dt className="font-semibold text-slate-900">Net GST</dt>
          <dd className="font-semibold text-slate-900">{currency(totals.netGst)}</dd>
        </div>
      </dl>
    </Card>
  )
}

export function GstReportPage() {
  const [range, setRange] = useState(currentMonthRange)
  const { data, loading, error } = useAsync(() => getGstReport(range.from, range.to), [range.from, range.to])

  return (
    <section>
      <PageHeader
        title="GST Report"
        description="Review only — these figures don't change what's filed or remitted. Included covers every account; Excluded leaves out Cash-type accounts, so Cash GST shows how much net GST currently sits in cash that hasn't been banked yet."
      />

      <div className="mb-6 flex flex-wrap items-end gap-4">
        <Field label="From">
          <input
            type="date"
            value={range.from}
            onChange={(e) => setRange((r) => ({ ...r, from: e.target.value }))}
            className={`${controlClass} w-44`}
          />
        </Field>
        <Field label="To">
          <input
            type="date"
            value={range.to}
            onChange={(e) => setRange((r) => ({ ...r, to: e.target.value }))}
            className={`${controlClass} w-44`}
          />
        </Field>
      </div>

      {loading && <StateMessage title="Loading GST report…" />}
      {error && <StateMessage title="Could not load GST report" description={error.message} />}

      {data && (
        <div className="space-y-6">
          <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
            <ScopeCard title="Included" description="GST across every account, cash and non-cash." totals={data.included} />
            <ScopeCard
              title="Excluded"
              description="GST across non-cash accounts only (Cash-type accounts left out)."
              totals={data.excluded}
            />
          </div>
          <Card title="Cash GST" className="max-w-sm">
            <p className="mb-3 text-sm text-slate-500">Net GST sitting in cash (Included − Excluded).</p>
            <p className="text-2xl font-semibold text-slate-900">{currency(data.cashGst)}</p>
          </Card>
        </div>
      )}
    </section>
  )
}
