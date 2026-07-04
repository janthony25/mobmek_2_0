import { useState } from 'react'
import { CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { getForecast } from '@/api/forecast'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { PageHeader } from '@/components/ui/PageHeader'
import { StateMessage } from '@/components/ui/StateMessage'
import { useAsync } from '@/hooks/useAsync'
import { currency, date as formatDate } from '@/lib/format'
import type { ForecastResult } from '@/types'

const HORIZONS = [
  { days: 30, label: '30 days' },
  { days: 90, label: '90 days' },
  { days: 365, label: '12 months' },
]

const MONTH_NAMES = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

interface Scenarios {
  best: ForecastResult
  expected: ForecastResult
  worst: ForecastResult
}

interface ChartPoint {
  label: string
  best: number | null
  expected: number | null
  worst: number | null
}

function buildChartData(data: Scenarios | null): ChartPoint[] {
  if (!data) return []
  const { best, expected, worst } = data

  if (expected.dailyPoints.length > 0) {
    return expected.dailyPoints.map((p, i) => ({
      label: formatDate(p.date),
      best: best.dailyPoints[i]?.closingBalance ?? null,
      expected: p.closingBalance,
      worst: worst.dailyPoints[i]?.closingBalance ?? null,
    }))
  }

  return expected.monthlyPoints.map((p, i) => ({
    label: `${MONTH_NAMES[p.month - 1]} ${p.year}`,
    best: best.monthlyPoints[i]?.closingBalance ?? null,
    expected: p.closingBalance,
    worst: worst.monthlyPoints[i]?.closingBalance ?? null,
  }))
}

export function ForecastPage() {
  const [horizonDays, setHorizonDays] = useState(90)
  const [showAssumptions, setShowAssumptions] = useState(false)

  const forecast = useAsync<Scenarios>(async () => {
    const [best, expected, worst] = await Promise.all([
      getForecast(horizonDays, 'BestCase'),
      getForecast(horizonDays, 'Expected'),
      getForecast(horizonDays, 'WorstCase'),
    ])
    return { best, expected, worst }
  }, [horizonDays])

  const chartData = buildChartData(forecast.data)
  const shortageDate = forecast.data?.expected.shortageDate ?? null

  return (
    <div>
      <PageHeader
        title="Forecast"
        description="Projected cash balance from current accounts, receivables, recurring schedules and planned items."
      />

      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <div className="inline-flex rounded-md border border-slate-300 bg-white p-0.5">
          {HORIZONS.map((h) => (
            <button
              key={h.days}
              type="button"
              onClick={() => setHorizonDays(h.days)}
              className={`rounded px-3 py-1.5 text-sm font-medium transition-colors ${
                horizonDays === h.days ? 'bg-slate-900 text-white' : 'text-slate-600 hover:bg-slate-100'
              }`}
            >
              {h.label}
            </button>
          ))}
        </div>
        <Button variant="secondary" size="sm" onClick={() => setShowAssumptions((v) => !v)}>
          {showAssumptions ? 'Hide assumptions' : 'Show assumptions'}
        </Button>
      </div>

      {shortageDate && (
        <div className="mb-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          <span className="font-semibold">Shortage warning —</span> the Expected balance is projected to drop below the
          safety buffer on <span className="font-semibold">{formatDate(shortageDate)}</span>.
        </div>
      )}

      {showAssumptions && (
        <Card title="Assumptions" className="mb-4">
          <ul className="list-disc space-y-1 pl-5 text-sm text-slate-600">
            <li>
              Receivables: unpaid invoices are expected on their due date, adjusted by each customer's typical payment
              delay (falling back to the business-wide median, then to no delay).
            </li>
            <li>
              <span className="font-medium text-green-700">Best case</span> — receivables collected in full on their
              due date; recurring income ×1.10; recurring expenses ×0.95; best-case planned items included.
            </li>
            <li>
              <span className="font-medium text-slate-700">Expected</span> — receivables per the payment-behaviour
              model above; recurring amounts as scheduled; always-tagged planned items only.
            </li>
            <li>
              <span className="font-medium text-red-600">Worst case</span> — receivables at 85% with 14 extra days'
              delay; recurring income ×0.85; recurring expenses ×1.10; worst-case planned items included.
            </li>
            <li>The shortage warning always reflects the Expected scenario, regardless of which lines you're viewing.</li>
            <li>Tax obligations aren't included yet — they land in a later phase of the cash-flow module.</li>
          </ul>
        </Card>
      )}

      <Card>
        {forecast.loading && chartData.length === 0 && <StateMessage title="Loading forecast…" loading />}
        {forecast.error && <StateMessage title="Could not load forecast" description={forecast.error.message} />}
        {chartData.length > 0 && (
          <div className="h-96 w-full">
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={chartData} margin={{ top: 10, right: 20, left: 0, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
                <XAxis dataKey="label" tick={{ fontSize: 12 }} minTickGap={24} />
                <YAxis tick={{ fontSize: 12 }} tickFormatter={(v: number) => currency(v)} width={90} />
                <Tooltip formatter={(value) => currency(Number(value))} />
                <Legend />
                <Line type="monotone" dataKey="best" name="Best case" stroke="#16a34a" strokeWidth={2} dot={false} />
                <Line type="monotone" dataKey="expected" name="Expected" stroke="#0f172a" strokeWidth={2} dot={false} />
                <Line type="monotone" dataKey="worst" name="Worst case" stroke="#dc2626" strokeWidth={2} dot={false} />
              </LineChart>
            </ResponsiveContainer>
          </div>
        )}
      </Card>
    </div>
  )
}
