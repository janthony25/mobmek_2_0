import { useState } from 'react'
import type { FormEvent } from 'react'
import {
  applyRuleToExisting,
  createCategorizationRule,
  deleteCategorizationRule,
  getCategorizationRules,
  updateCategorizationRule,
} from '@/api/categorizationRules'
import { getPayees } from '@/api/payees'
import { getTransactionCategories } from '@/api/transactionCategories'
import { CrudSection } from '@/components/crud/CrudSection'
import { Button } from '@/components/ui/Button'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { StateMessage } from '@/components/ui/StateMessage'
import { useToast } from '@/components/ui/toast'
import { Field, controlClass } from '@/components/forms/controls'
import { useAsync } from '@/hooks/useAsync'
import type {
  ApplyRuleResult,
  CategorizationRule,
  CategorizationRuleRequest,
  Payee,
  TransactionCategory,
} from '@/types'
import { RULE_MATCH_FIELD_LABELS, RULE_MATCH_TYPE_LABELS } from '@/types'

export function CategorizationRulesPage() {
  const toast = useToast()
  const categories = useAsync(() => getTransactionCategories(), [])
  const payees = useAsync(() => getPayees(), [])
  const [reloadKey, setReloadKey] = useState(0)
  const [applying, setApplying] = useState<{ rule: CategorizationRule; preview: ApplyRuleResult } | null>(null)

  if (categories.loading || payees.loading) return <StateMessage title="Loading…" />
  if (categories.error) return <StateMessage title="Could not load categories" description={categories.error.message} />

  const startApply = async (rule: CategorizationRule) => {
    const preview = await applyRuleToExisting(rule.id, false)
    if (preview.updatedCount === 0) {
      toast.success(
        preview.matchCount === 0
          ? 'No existing transactions match this rule.'
          : 'All matching transactions already have this rule’s outcome.',
      )
      return
    }
    setApplying({ rule, preview })
  }

  const commitApply = async () => {
    if (!applying) return
    try {
      const result = await applyRuleToExisting(applying.rule.id, true)
      toast.success(`${result.updatedCount} transaction${result.updatedCount === 1 ? '' : 's'} recategorized`)
      setReloadKey((k) => k + 1)
    } catch (err) {
      toast.error(err instanceof Error ? err.message : String(err))
    } finally {
      setApplying(null)
    }
  }

  return (
    <>
      <CrudSection<CategorizationRule>
        resourceName="Rule"
        title="Categorization Rules"
        description="Automatic categorization: when a transaction's text matches a rule, its category (and optionally GST treatment and payee) is filled in — on entry, on statement import, and retroactively via “Apply to history”. The lowest priority number wins when several rules match."
        load={getCategorizationRules}
        reloadKey={reloadKey}
        getId={(r) => r.id}
        rowLabel={(r) => r.name}
        columns={[
          {
            header: 'Rule',
            cell: (r) => (
              <>
                {r.name}
                {!r.isActive && (
                  <span className="ml-2 rounded bg-slate-100 px-1.5 py-0.5 text-xs text-slate-500">inactive</span>
                )}
              </>
            ),
            className: 'font-medium text-slate-900',
          },
          { header: 'Priority', cell: (r) => String(r.priority) },
          {
            header: 'When',
            cell: (r) =>
              `${RULE_MATCH_FIELD_LABELS[r.matchField] ?? r.matchField} ${(RULE_MATCH_TYPE_LABELS[r.matchType] ?? r.matchType).toLowerCase()} “${r.matchValue}”` +
              (r.direction ? ` · ${r.direction === 'In' ? 'money in' : 'money out'}` : ''),
          },
          {
            header: 'Then',
            cell: (r) =>
              r.setCategoryName +
              (r.setGstTreatment ? ` · GST ${r.setGstTreatment}` : '') +
              (r.setPayeeName ? ` · payee ${r.setPayeeName}` : ''),
          },
        ]}
        renderForm={({ initial, onSubmit, onCancel }) => (
          <RuleForm
            initial={initial}
            categories={categories.data ?? []}
            payees={payees.data ?? []}
            onSubmit={onSubmit}
            onCancel={onCancel}
          />
        )}
        onCreate={(v) => createCategorizationRule(v as unknown as CategorizationRuleRequest).then(() => undefined)}
        onUpdate={(id, v) => updateCategorizationRule(id, v as unknown as CategorizationRuleRequest).then(() => undefined)}
        onDelete={deleteCategorizationRule}
        extraAction={{
          label: () => 'Apply to history',
          onClick: startApply,
        }}
        emptyText="No rules yet. Example: description contains “z energy” → Vehicle & Fuel."
      />

      <ConfirmDialog
        open={applying !== null}
        title="Apply Rule to Existing Transactions"
        message={
          applying
            ? `“${applying.rule.name}” matches ${applying.preview.matchCount} transaction${applying.preview.matchCount === 1 ? '' : 's'}; ${applying.preview.updatedCount} would be recategorized (invoice-posted, transfer, reconciled and locked rows are always left alone). Apply it?`
            : ''
        }
        onConfirm={commitApply}
        onCancel={() => setApplying(null)}
      />
    </>
  )
}

function RuleForm({
  initial,
  categories,
  payees,
  onSubmit,
  onCancel,
}: {
  initial: CategorizationRule | null
  categories: TransactionCategory[]
  payees: Payee[]
  onSubmit: (values: Record<string, unknown>) => Promise<void>
  onCancel: () => void
}) {
  const [name, setName] = useState(initial?.name ?? '')
  const [priority, setPriority] = useState(String(initial?.priority ?? 10))
  const [matchField, setMatchField] = useState(initial?.matchField ?? 'Either')
  const [matchType, setMatchType] = useState(initial?.matchType ?? 'Contains')
  const [matchValue, setMatchValue] = useState(initial?.matchValue ?? '')
  const [direction, setDirection] = useState(initial?.direction ?? '')
  const [amountMin, setAmountMin] = useState(initial?.amountMin != null ? String(initial.amountMin) : '')
  const [amountMax, setAmountMax] = useState(initial?.amountMax != null ? String(initial.amountMax) : '')
  const [setCategoryId, setSetCategoryId] = useState(initial?.setCategoryId ?? '')
  const [setGstTreatment, setSetGstTreatment] = useState(initial?.setGstTreatment ?? '')
  const [setPayeeId, setSetPayeeId] = useState(initial?.setPayeeId ?? '')
  const [isActive, setIsActive] = useState(initial?.isActive ?? true)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)
    setBusy(true)
    try {
      await onSubmit({
        name: name.trim(),
        priority: Number(priority),
        matchField,
        matchType,
        matchValue: matchValue.trim(),
        direction: direction || null,
        amountMin: amountMin ? Number(amountMin) : null,
        amountMax: amountMax ? Number(amountMax) : null,
        setCategoryId,
        setGstTreatment: setGstTreatment || null,
        setPayeeId: setPayeeId || null,
        isActive,
      })
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div className="grid grid-cols-2 gap-4">
        <Field label="Name" required>
          <input type="text" required maxLength={200} value={name} onChange={(e) => setName(e.target.value)} className={controlClass} />
        </Field>
        <Field label="Priority" required>
          <input
            type="number" required min={0} step="1" value={priority}
            onChange={(e) => setPriority(e.target.value)} className={controlClass}
          />
        </Field>
      </div>

      <fieldset className="rounded-md border border-slate-200 p-3">
        <legend className="px-1 text-xs font-medium uppercase tracking-wide text-slate-400">When</legend>
        <div className="grid grid-cols-2 gap-4">
          <Field label="Look at" required>
            <select value={matchField} onChange={(e) => setMatchField(e.target.value)} className={controlClass}>
              <option value="Either">Description or counterparty</option>
              <option value="Description">Description</option>
              <option value="Counterparty">Counterparty</option>
            </select>
          </Field>
          <Field label="Match type" required>
            <select value={matchType} onChange={(e) => setMatchType(e.target.value)} className={controlClass}>
              <option value="Contains">Contains</option>
              <option value="StartsWith">Starts with</option>
              <option value="Equals">Equals</option>
            </select>
          </Field>
        </div>
        <Field label="Text to match" required>
          <input type="text" required maxLength={200} value={matchValue} onChange={(e) => setMatchValue(e.target.value)} className={controlClass} />
        </Field>
        <div className="grid grid-cols-3 gap-4">
          <Field label="Direction">
            <select value={direction} onChange={(e) => setDirection(e.target.value)} className={controlClass}>
              <option value="">Either</option>
              <option value="In">Money in</option>
              <option value="Out">Money out</option>
            </select>
          </Field>
          <Field label="Amount from">
            <input type="number" step="0.01" min="0" value={amountMin} onChange={(e) => setAmountMin(e.target.value)} className={controlClass} />
          </Field>
          <Field label="Amount to">
            <input type="number" step="0.01" min="0" value={amountMax} onChange={(e) => setAmountMax(e.target.value)} className={controlClass} />
          </Field>
        </div>
      </fieldset>

      <fieldset className="rounded-md border border-slate-200 p-3">
        <legend className="px-1 text-xs font-medium uppercase tracking-wide text-slate-400">Then set</legend>
        <div className="grid grid-cols-3 gap-4">
          <Field label="Category" required>
            <select required value={setCategoryId} onChange={(e) => setSetCategoryId(e.target.value)} className={controlClass}>
              <option value="" disabled>Select…</option>
              {categories.filter((c) => !c.isArchived).map((c) => (
                <option key={c.id} value={c.id}>{c.name}</option>
              ))}
            </select>
          </Field>
          <Field label="GST treatment">
            <select value={setGstTreatment} onChange={(e) => setSetGstTreatment(e.target.value)} className={controlClass}>
              <option value="">Leave as is</option>
              <option value="Taxable">Taxable</option>
              <option value="Exempt">Exempt</option>
              <option value="ZeroRated">Zero-rated</option>
            </select>
          </Field>
          <Field label="Payee">
            <select value={setPayeeId} onChange={(e) => setSetPayeeId(e.target.value)} className={controlClass}>
              <option value="">Leave as is</option>
              {payees.map((p) => (
                <option key={p.id} value={p.id}>{p.name}</option>
              ))}
            </select>
          </Field>
        </div>
      </fieldset>

      <label className="flex items-center gap-2 text-sm text-slate-700">
        <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
        Active
      </label>

      {error && <p className="text-sm text-red-600">{error}</p>}
      <div className="flex justify-end gap-2 pt-1">
        <Button type="button" variant="secondary" onClick={onCancel} disabled={busy}>Cancel</Button>
        <Button type="submit" disabled={busy}>{busy ? 'Saving…' : initial ? 'Save changes' : 'Create rule'}</Button>
      </div>
    </form>
  )
}
