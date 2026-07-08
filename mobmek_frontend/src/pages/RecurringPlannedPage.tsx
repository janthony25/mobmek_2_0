import { useState } from 'react'
import type { FormEvent } from 'react'
import { getCashAccounts } from '@/api/cashAccounts'
import { getTransactionCategories } from '@/api/transactionCategories'
import {
  createRecurringTransaction,
  deleteRecurringTransaction,
  getDueOccurrences,
  getRecurringTransactions,
  postRecurringOccurrence,
  setRecurringTransactionPaused,
  updateRecurringTransaction,
} from '@/api/recurringTransactions'
import {
  createPlannedTransaction,
  deletePlannedTransaction,
  getPlannedTransactions,
  updatePlannedTransaction,
} from '@/api/plannedTransactions'
import { CrudSection } from '@/components/crud/CrudSection'
import type { CrudFormProps } from '@/components/crud/CrudSection'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { PageHeader } from '@/components/ui/PageHeader'
import { StateMessage } from '@/components/ui/StateMessage'
import { useToast } from '@/components/ui/toast'
import { Field, controlClass } from '@/components/forms/controls'
import { useAsync } from '@/hooks/useAsync'
import { currency, date as formatDate } from '@/lib/format'
import type {
  CashAccount,
  CreatePlannedTransactionRequest,
  DueOccurrence,
  PlannedTransaction,
  RecurringTransaction,
  RecurringTransactionRequest,
  TransactionCategory,
  UpdatePlannedTransactionRequest,
} from '@/types'
import {
  PLANNED_TRANSACTION_SCENARIO_LABELS,
  PLANNED_TRANSACTION_SCENARIO_TAGS,
  RECURRING_FREQUENCIES,
  RECURRING_FREQUENCY_LABELS,
} from '@/types'

const GST_OPTIONS = ['Taxable', 'Exempt', 'ZeroRated'] as const
const today = () => new Date().toISOString().slice(0, 10)

const iso90DaysOut = () => {
  const d = new Date()
  d.setDate(d.getDate() + 90)
  return d.toISOString().slice(0, 10)
}

const amountBadge = (direction: string, amount: number) => (
  <span className={direction === 'In' ? 'font-medium text-green-700' : 'font-medium text-red-600'}>
    {direction === 'In' ? '+' : '-'}
    {currency(amount)}
  </span>
)

const FREQUENCY_UNITS: Record<string, string> = {
  Weekly: 'week',
  Fortnightly: 'fortnight',
  Monthly: 'month',
  Quarterly: 'quarter',
  Annually: 'year',
}

/** "Monthly", "Every 2 months" — human cadence instead of "Monthly (×2)". */
const cadenceText = (r: RecurringTransaction) =>
  r.interval > 1
    ? `Every ${r.interval} ${FREQUENCY_UNITS[r.frequency] ?? r.frequency.toLowerCase()}s`
    : RECURRING_FREQUENCY_LABELS[r.frequency] ?? r.frequency

export function RecurringPlannedPage() {
  const toast = useToast()
  const [version, setVersion] = useState(0)
  const changed = () => setVersion((v) => v + 1)

  const accounts = useAsync(() => getCashAccounts(), [version])
  const categories = useAsync(() => getTransactionCategories(), [version])
  const due = useAsync(() => getDueOccurrences(), [version])
  // Loaded at page level (as well as inside the sections) to drive the commitment summary.
  const recurring = useAsync(() => getRecurringTransactions(), [version])
  const planned = useAsync(getPlannedTransactions, [version])

  const postNow = async (occurrence: DueOccurrence) => {
    try {
      await postRecurringOccurrence(occurrence.recurringTransactionId, occurrence.date)
      toast.success(`“${occurrence.description}” posted to the ledger`)
      changed()
    } catch (err) {
      toast.error(err instanceof Error ? err.message : String(err))
    }
  }

  const active = (recurring.data ?? []).filter((r) => !r.isPaused)
  const monthlyIn = active.filter((r) => r.direction === 'In').reduce((s, r) => s + r.monthlyEquivalentAmount, 0)
  const monthlyOut = active.filter((r) => r.direction === 'Out').reduce((s, r) => s + r.monthlyEquivalentAmount, 0)

  const todayIso = today()
  const in90DaysIso = iso90DaysOut()
  const upcomingPlanned = (planned.data ?? []).filter(
    (p) => p.status === 'Planned' && p.expectedDate >= todayIso && p.expectedDate <= in90DaysIso,
  )
  const plannedNet = upcomingPlanned.reduce((s, p) => s + (p.direction === 'In' ? p.amount : -p.amount), 0)

  const dueCount = due.data?.length ?? 0
  const dueNet = (due.data ?? []).reduce((s, o) => s + (o.direction === 'In' ? o.amount : -o.amount), 0)

  return (
    <div className="space-y-10">
      <PageHeader
        title="Recurring & Planned"
        description="Committed regular income/expenses and known future one-offs — the building blocks the forecast projects from."
      />

      {/* Commitment summary */}
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 xl:grid-cols-5">
        <StatCard
          label="Due to confirm"
          value={dueCount === 0 ? 'Nothing due' : `${dueCount} item${dueCount === 1 ? '' : 's'}`}
          sub={dueCount > 0 ? `Net effect ${currency(dueNet)}` : 'All caught up'}
          tone={dueCount > 0 ? 'warn' : 'ok'}
        />
        <StatCard label="Recurring income" value={currency(monthlyIn)} sub="per month, active schedules" tone="in" />
        <StatCard label="Recurring costs" value={currency(monthlyOut)} sub="per month, active schedules" tone="out" />
        <StatCard
          label="Net per month"
          value={currency(monthlyIn - monthlyOut)}
          sub="recurring income − costs"
          tone={monthlyIn - monthlyOut >= 0 ? 'in' : 'out'}
        />
        <StatCard
          label="Planned (90 days)"
          value={upcomingPlanned.length === 0 ? 'None' : currency(plannedNet)}
          sub={upcomingPlanned.length === 0 ? 'nothing scheduled' : `${upcomingPlanned.length} one-off item${upcomingPlanned.length === 1 ? '' : 's'}`}
          tone={plannedNet >= 0 ? 'in' : 'out'}
        />
      </div>

      <Card title="Due — confirm">
        <p className="mb-3 text-sm text-slate-500">
          These schedule occurrences have reached their date but aren't in the ledger yet. Posting one records the
          actual cash movement; schedules set to auto-post skip this queue.
        </p>
        {due.loading && !due.data && <StateMessage title="Loading due occurrences…" loading />}
        {due.error && <StateMessage title="Could not load due occurrences" description={due.error.message} />}
        {due.data && due.data.length === 0 && (
          <StateMessage title="Nothing due" description="Occurrences appear here once their date arrives." />
        )}
        {due.data && due.data.length > 0 && (
          <div className="divide-y divide-slate-100">
            {due.data.map((o) => (
              <div key={`${o.recurringTransactionId}-${o.date}`} className="flex items-center justify-between gap-4 py-3">
                <div>
                  <p className="font-medium text-slate-900">
                    {o.description}
                    {o.date < todayIso && <span className="ml-2 inline-block align-middle"><Badge tone="amber">overdue</Badge></span>}
                  </p>
                  <p className="text-sm text-slate-500">
                    Due {formatDate(o.date)} · {o.accountName}
                  </p>
                </div>
                <div className="flex items-center gap-3">
                  {amountBadge(o.direction, o.amount)}
                  <Button size="sm" onClick={() => postNow(o)}>Post to ledger</Button>
                </div>
              </div>
            ))}
          </div>
        )}
      </Card>

      <CrudSection<RecurringTransaction>
        resourceName="Schedule"
        title="Recurring Schedules"
        description="Rent, insurance, subscriptions, retainer income — anything on a regular cycle."
        load={() => getRecurringTransactions()}
        reloadKey={version}
        getId={(r) => r.id}
        rowLabel={(r) => r.description}
        columns={[
          {
            header: 'Schedule',
            cell: (r) => (
              <div>
                <p className="font-medium text-slate-900">{r.description}</p>
                <p className="text-xs text-slate-500">
                  {[r.counterparty, r.categoryName].filter(Boolean).join(' · ')}
                </p>
              </div>
            ),
          },
          {
            header: 'Amount',
            cell: (r) => (
              <div>
                {amountBadge(r.direction, r.amount)}
                <p className="text-xs text-slate-500">≈ {currency(r.monthlyEquivalentAmount)}/month</p>
              </div>
            ),
          },
          {
            header: 'Cadence',
            cell: (r) => (
              <div>
                <p className="text-slate-700">{cadenceText(r)}</p>
                <p className="text-xs text-slate-500">
                  {r.isPaused
                    ? 'Paused — not forecast'
                    : r.nextOccurrenceDate
                      ? `Next ${formatDate(r.nextOccurrenceDate)}`
                      : 'Ended'}
                  {r.endDate ? ` · ends ${formatDate(r.endDate)}` : ''}
                </p>
              </div>
            ),
          },
          { header: 'Account', cell: (r) => r.accountName },
          {
            header: 'Posting',
            cell: (r) => (
              <div className="flex flex-wrap gap-1">
                {r.isPaused && <Badge tone="slate">Paused</Badge>}
                {r.autoPost
                  ? <Badge tone="blue">Auto-posts</Badge>
                  : <Badge tone="amber">Confirm each time</Badge>}
              </div>
            ),
          },
        ]}
        renderForm={(props) => (
          <RecurringForm {...props} accounts={accounts.data ?? []} categories={categories.data ?? []} />
        )}
        onCreate={(v) => createRecurringTransaction(v as unknown as RecurringTransactionRequest).then(() => undefined)}
        onUpdate={(id, v) => updateRecurringTransaction(id, v as unknown as RecurringTransactionRequest).then(() => undefined)}
        onDelete={deleteRecurringTransaction}
        onChanged={changed}
        extraAction={{
          label: (r) => (r.isPaused ? 'Resume' : 'Pause'),
          onClick: (r) => setRecurringTransactionPaused(r.id, !r.isPaused).then(() => undefined),
        }}
        emptyText="No recurring schedules yet"
      />

      <CrudSection<PlannedTransaction>
        resourceName="Planned Item"
        title="Planned One-offs"
        description="Known future one-off items (equipment purchases, expected grants) not already covered by a schedule or an invoice."
        load={getPlannedTransactions}
        reloadKey={version}
        getId={(p) => p.id}
        rowLabel={(p) => p.description}
        columns={[
          {
            header: 'Item',
            cell: (p) => (
              <div>
                <p className="font-medium text-slate-900">{p.description}</p>
                <p className="text-xs text-slate-500">{[p.accountName ?? 'Account not decided', p.categoryName].join(' · ')}</p>
              </div>
            ),
          },
          { header: 'Amount', cell: (p) => amountBadge(p.direction, p.amount) },
          {
            header: 'Expected',
            cell: (p) => (
              <div>
                {formatDate(p.expectedDate)}
                {p.status === 'Planned' && p.expectedDate < today() && (
                  <span className="ml-2 inline-block align-middle"><Badge tone="amber">date passed</Badge></span>
                )}
              </div>
            ),
          },
          {
            header: 'Scenario',
            cell: (p) => (
              <span title={p.scenarioTag ? 'Only counted in that forecast scenario — a what-if item' : 'Counted in every forecast scenario'}>
                <Badge tone={p.scenarioTag ? 'amber' : 'slate'}>
                  {p.scenarioTag ? PLANNED_TRANSACTION_SCENARIO_LABELS[p.scenarioTag] : 'Always'}
                </Badge>
              </span>
            ),
          },
          {
            header: 'Status',
            cell: (p) => (
              <Badge tone={p.status === 'Planned' ? 'blue' : p.status === 'Posted' ? 'green' : 'slate'}>
                {p.status}
              </Badge>
            ),
          },
        ]}
        renderForm={(props) => (
          <PlannedForm {...props} accounts={accounts.data ?? []} categories={categories.data ?? []} />
        )}
        onCreate={(v) => createPlannedTransaction(v as unknown as CreatePlannedTransactionRequest).then(() => undefined)}
        onUpdate={(id, v) => updatePlannedTransaction(id, v as unknown as UpdatePlannedTransactionRequest).then(() => undefined)}
        onDelete={deletePlannedTransaction}
        onChanged={changed}
        extraAction={{
          label: () => 'Mark posted',
          hidden: (p) => p.status !== 'Planned',
          onClick: (p) =>
            updatePlannedTransaction(p.id, {
              description: p.description,
              direction: p.direction,
              amount: p.amount,
              expectedDate: p.expectedDate,
              categoryId: p.categoryId,
              accountId: p.accountId,
              scenarioTag: p.scenarioTag,
              status: 'Posted',
            }).then(() => undefined),
        }}
        emptyText="No planned items yet"
      />
    </div>
  )
}

function StatCard({
  label,
  value,
  sub,
  tone,
}: {
  label: string
  value: string
  sub?: string
  tone?: 'in' | 'out' | 'warn' | 'ok'
}) {
  const valueColor =
    tone === 'in' ? 'text-emerald-600' : tone === 'out' ? 'text-red-600' : tone === 'warn' ? 'text-amber-700' : 'text-slate-900'
  return (
    <div className={`rounded-lg border px-4 py-3 ${tone === 'warn' ? 'border-amber-300 bg-amber-50' : 'border-slate-200 bg-white'}`}>
      <p className="truncate text-xs font-medium uppercase tracking-wide text-slate-500">{label}</p>
      <p className={`mt-1 text-lg font-semibold ${valueColor}`}>{value}</p>
      {sub && <p className="truncate text-xs text-slate-400">{sub}</p>}
    </div>
  )
}

// --- Recurring schedule form -------------------------------------------------------

function RecurringForm({
  initial,
  onSubmit,
  onCancel,
  accounts,
  categories,
}: CrudFormProps<RecurringTransaction> & { accounts: CashAccount[]; categories: TransactionCategory[] }) {
  const [description, setDescription] = useState(initial?.description ?? '')
  const [direction, setDirection] = useState(initial?.direction ?? 'Out')
  const [amount, setAmount] = useState(initial ? String(initial.amount) : '')
  const [categoryId, setCategoryId] = useState(initial?.categoryId ?? '')
  const [accountId, setAccountId] = useState(initial?.accountId ?? accounts[0]?.id ?? '')
  const [counterparty, setCounterparty] = useState(initial?.counterparty ?? '')
  const [gstTreatment, setGstTreatment] = useState(initial?.gstTreatment ?? '')
  const [frequency, setFrequency] = useState(initial?.frequency ?? 'Monthly')
  const [intervalCount, setIntervalCount] = useState(initial ? String(initial.interval) : '1')
  const [anchorDate, setAnchorDate] = useState(initial?.anchorDate ?? today())
  const [endDate, setEndDate] = useState(initial?.endDate ?? '')
  const [autoPost, setAutoPost] = useState(initial?.autoPost ?? false)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const visibleCategories = categories.filter(
    (c) => !c.isArchived && (c.direction === 'Either' || c.direction === direction),
  )

  const changeDirection = (next: string) => {
    setDirection(next)
    const current = categories.find((c) => c.id === categoryId)
    if (current && current.direction !== 'Either' && current.direction !== next) {
      setCategoryId('')
    }
  }

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)
    setBusy(true)
    try {
      await onSubmit({
        description: description.trim(),
        direction,
        amount: Number(amount),
        categoryId,
        accountId,
        counterparty: counterparty.trim() || null,
        gstTreatment: gstTreatment || null,
        frequency,
        interval: Number(intervalCount),
        anchorDate,
        endDate: endDate || null,
        autoPost,
        isPaused: initial?.isPaused ?? false,
      })
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <Field label="Description" required>
        <input
          type="text" required maxLength={500} value={description}
          onChange={(e) => setDescription(e.target.value.toUpperCase())} className={controlClass}
        />
      </Field>
      <div className="grid grid-cols-2 gap-4">
        <Field label="Direction" required>
          <select value={direction} onChange={(e) => changeDirection(e.target.value)} className={controlClass}>
            <option value="In">Money in</option>
            <option value="Out">Money out</option>
          </select>
        </Field>
        <Field label="Amount" required>
          <input
            type="number" step="0.01" min="0.01" required value={amount}
            onChange={(e) => setAmount(e.target.value)} className={controlClass}
          />
        </Field>
        <Field label="Category" required>
          <select required value={categoryId} onChange={(e) => setCategoryId(e.target.value)} className={controlClass}>
            <option value="" disabled>Select a category…</option>
            {visibleCategories.map((c) => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </select>
        </Field>
        <Field label="Account" required>
          <select required value={accountId} onChange={(e) => setAccountId(e.target.value)} className={controlClass}>
            {accounts.map((a) => (
              <option key={a.id} value={a.id}>{a.name}</option>
            ))}
          </select>
        </Field>
      </div>
      <div className="grid grid-cols-2 gap-4">
        <Field label="Frequency" required>
          <select value={frequency} onChange={(e) => setFrequency(e.target.value)} className={controlClass}>
            {RECURRING_FREQUENCIES.map((f) => (
              <option key={f} value={f}>{RECURRING_FREQUENCY_LABELS[f]}</option>
            ))}
          </select>
        </Field>
        <Field label="Every N periods" required>
          <input
            type="number" min="1" step="1" required value={intervalCount}
            onChange={(e) => setIntervalCount(e.target.value)} className={controlClass}
          />
        </Field>
        <Field label="First occurrence" required>
          <input type="date" required value={anchorDate} onChange={(e) => setAnchorDate(e.target.value)} className={controlClass} />
        </Field>
        <Field label="Ends">
          <input type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} className={controlClass} />
        </Field>
      </div>
      <div className="grid grid-cols-2 gap-4">
        <Field label="Payee / payer">
          <input
            type="text" maxLength={200} value={counterparty}
            onChange={(e) => setCounterparty(e.target.value.toUpperCase())} className={controlClass}
          />
        </Field>
        <Field label="GST treatment">
          <select value={gstTreatment} onChange={(e) => setGstTreatment(e.target.value)} className={controlClass}>
            <option value="">Category default</option>
            {GST_OPTIONS.map((g) => (
              <option key={g} value={g}>{g === 'ZeroRated' ? 'Zero-rated' : g}</option>
            ))}
          </select>
        </Field>
      </div>
      <label className="flex items-center gap-2 text-sm font-medium text-slate-700">
        <input type="checkbox" checked={autoPost} onChange={(e) => setAutoPost(e.target.checked)} />
        Auto-post occurrences (otherwise they wait in the due queue for a manual confirm)
      </label>
      {error && <p className="text-sm text-red-600">{error}</p>}
      <div className="flex justify-end gap-2 pt-1">
        <Button type="button" variant="secondary" onClick={onCancel} disabled={busy}>Cancel</Button>
        <Button type="submit" disabled={busy}>{busy ? 'Saving…' : initial ? 'Save changes' : 'Create schedule'}</Button>
      </div>
    </form>
  )
}

// --- Planned one-off form -----------------------------------------------------------

function PlannedForm({
  initial,
  onSubmit,
  onCancel,
  accounts,
  categories,
}: CrudFormProps<PlannedTransaction> & { accounts: CashAccount[]; categories: TransactionCategory[] }) {
  const [description, setDescription] = useState(initial?.description ?? '')
  const [direction, setDirection] = useState(initial?.direction ?? 'Out')
  const [amount, setAmount] = useState(initial ? String(initial.amount) : '')
  const [expectedDate, setExpectedDate] = useState(initial?.expectedDate ?? today())
  const [categoryId, setCategoryId] = useState(initial?.categoryId ?? '')
  const [accountId, setAccountId] = useState(initial?.accountId ?? '')
  const [scenarioTag, setScenarioTag] = useState(initial?.scenarioTag ?? '')
  const [status, setStatus] = useState(initial?.status ?? 'Planned')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const visibleCategories = categories.filter(
    (c) => !c.isArchived && (c.direction === 'Either' || c.direction === direction),
  )

  const changeDirection = (next: string) => {
    setDirection(next)
    const current = categories.find((c) => c.id === categoryId)
    if (current && current.direction !== 'Either' && current.direction !== next) {
      setCategoryId('')
    }
  }

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)
    setBusy(true)
    try {
      const base = {
        description: description.trim(),
        direction,
        amount: Number(amount),
        expectedDate,
        categoryId,
        accountId: accountId || null,
        scenarioTag: scenarioTag || null,
      }
      await onSubmit(initial ? { ...base, status } : base)
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <Field label="Description" required>
        <input
          type="text" required maxLength={500} value={description}
          onChange={(e) => setDescription(e.target.value.toUpperCase())} className={controlClass}
        />
      </Field>
      <div className="grid grid-cols-2 gap-4">
        <Field label="Direction" required>
          <select value={direction} onChange={(e) => changeDirection(e.target.value)} className={controlClass}>
            <option value="In">Money in</option>
            <option value="Out">Money out</option>
          </select>
        </Field>
        <Field label="Amount" required>
          <input
            type="number" step="0.01" min="0.01" required value={amount}
            onChange={(e) => setAmount(e.target.value)} className={controlClass}
          />
        </Field>
        <Field label="Expected date" required>
          <input type="date" required value={expectedDate} onChange={(e) => setExpectedDate(e.target.value)} className={controlClass} />
        </Field>
        <Field label="Category" required>
          <select required value={categoryId} onChange={(e) => setCategoryId(e.target.value)} className={controlClass}>
            <option value="" disabled>Select a category…</option>
            {visibleCategories.map((c) => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </select>
        </Field>
        <Field label="Account">
          <select value={accountId} onChange={(e) => setAccountId(e.target.value)} className={controlClass}>
            <option value="">Not yet decided</option>
            {accounts.map((a) => (
              <option key={a.id} value={a.id}>{a.name}</option>
            ))}
          </select>
        </Field>
        <Field label="Scenario">
          <select value={scenarioTag} onChange={(e) => setScenarioTag(e.target.value)} className={controlClass}>
            <option value="">Always</option>
            {PLANNED_TRANSACTION_SCENARIO_TAGS.map((tag) => (
              <option key={tag} value={tag}>{PLANNED_TRANSACTION_SCENARIO_LABELS[tag]}</option>
            ))}
          </select>
        </Field>
      </div>
      {initial && (
        <Field label="Status" required>
          <select required value={status} onChange={(e) => setStatus(e.target.value)} className={controlClass}>
            <option value="Planned">Planned</option>
            <option value="Posted">Posted</option>
            <option value="Cancelled">Cancelled</option>
          </select>
        </Field>
      )}
      {error && <p className="text-sm text-red-600">{error}</p>}
      <div className="flex justify-end gap-2 pt-1">
        <Button type="button" variant="secondary" onClick={onCancel} disabled={busy}>Cancel</Button>
        <Button type="submit" disabled={busy}>{busy ? 'Saving…' : initial ? 'Save changes' : 'Create planned item'}</Button>
      </div>
    </form>
  )
}
