import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { getCashAccounts } from '@/api/cashAccounts'
import { getTransactionCategories } from '@/api/transactionCategories'
import { getPayees } from '@/api/payees'
import { suggestCategorization } from '@/api/categorizationRules'
import { getAuditTrail } from '@/api/cashFlowAudit'
import {
  addTransactionAttachment,
  bulkCashTransactions,
  cashTransactionsExportUrl,
  createCashTransaction,
  createSplitTransaction,
  createTransfer,
  deleteCashTransaction,
  deleteTransactionAttachment,
  getCashTransaction,
  getCashTransactions,
  transactionAttachmentUrl,
  updateCashTransaction,
  updateSplitTransaction,
} from '@/api/cashTransactions'
import type { CashTransactionQuery } from '@/api/cashTransactions'
import { Button } from '@/components/ui/Button'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { Modal } from '@/components/ui/Modal'
import { PageHeader } from '@/components/ui/PageHeader'
import { StateMessage } from '@/components/ui/StateMessage'
import { useToast } from '@/components/ui/toast'
import { Field, controlClass } from '@/components/forms/controls'
import { useAsync } from '@/hooks/useAsync'
import { currency, date as formatDate, orDash } from '@/lib/format'
import type {
  BulkTransactionAction,
  BulkTransactionResult,
  CashAccount,
  CashFlowAuditLog,
  CashTransaction,
  CashTransactionRequest,
  Payee,
  SplitTransactionRequest,
  TransactionCategory,
} from '@/types'
import { CASH_ACCOUNT_TYPE_LABELS } from '@/types'

const PAGE_SIZE = 25

const GST_OPTIONS = ['Taxable', 'Exempt', 'ZeroRated'] as const

const today = () => new Date().toISOString().slice(0, 10)

interface Filters {
  accountId: string
  categoryId: string
  payeeId: string
  direction: string
  status: string
  from: string
  to: string
  search: string
}

const EMPTY_FILTERS: Filters = {
  accountId: '', categoryId: '', payeeId: '', direction: '', status: '', from: '', to: '', search: '',
}

// --- Date-range presets -----------------------------------------------------------

const iso = (d: Date) => d.toISOString().slice(0, 10)

/** NZ financial year starts 1 April. */
const DATE_PRESETS: Record<string, () => { from: string; to: string }> = {
  thisMonth: () => {
    const now = new Date()
    return { from: iso(new Date(Date.UTC(now.getFullYear(), now.getMonth(), 1))), to: today() }
  },
  lastMonth: () => {
    const now = new Date()
    return {
      from: iso(new Date(Date.UTC(now.getFullYear(), now.getMonth() - 1, 1))),
      to: iso(new Date(Date.UTC(now.getFullYear(), now.getMonth(), 0))),
    }
  },
  thisQuarter: () => {
    const now = new Date()
    const quarterStart = Math.floor(now.getMonth() / 3) * 3
    return { from: iso(new Date(Date.UTC(now.getFullYear(), quarterStart, 1))), to: today() }
  },
  fyToDate: () => {
    const now = new Date()
    const fyYear = now.getMonth() >= 3 ? now.getFullYear() : now.getFullYear() - 1
    return { from: iso(new Date(Date.UTC(fyYear, 3, 1))), to: today() }
  },
  last12Months: () => {
    const now = new Date()
    return { from: iso(new Date(Date.UTC(now.getFullYear() - 1, now.getMonth(), now.getDate()))), to: today() }
  },
  allTime: () => ({ from: '', to: '' }),
}

export function CashFlowPage() {
  const toast = useToast()
  const [filters, setFilters] = useState<Filters>(EMPTY_FILTERS)
  const [preset, setPreset] = useState('allTime')
  const [page, setPage] = useState(1)
  // Bumped after any mutation so the ledger, totals and account balances refresh together.
  const [version, setVersion] = useState(0)
  const [selected, setSelected] = useState<Set<string>>(new Set())

  const query: CashTransactionQuery = {
    accountId: filters.accountId || undefined,
    categoryId: filters.categoryId || undefined,
    payeeId: filters.payeeId || undefined,
    direction: filters.direction || undefined,
    status: filters.status || undefined,
    from: filters.from || undefined,
    to: filters.to || undefined,
    search: filters.search || undefined,
  }

  const accounts = useAsync(() => getCashAccounts(), [version])
  const categories = useAsync(() => getTransactionCategories(), [version])
  const payees = useAsync(() => getPayees(), [version])
  const ledger = useAsync(
    () => getCashTransactions({ ...query, page, pageSize: PAGE_SIZE }),
    [filters, page, version],
  )

  const [showFilters, setShowFilters] = useState(false)
  const [editing, setEditing] = useState<{ mode: 'create' } | { mode: 'edit'; row: CashTransaction } | null>(null)
  const [transferring, setTransferring] = useState(false)
  const [splitting, setSplitting] = useState<{ mode: 'create' } | { mode: 'edit'; groupId: string } | null>(null)
  const [deleting, setDeleting] = useState<CashTransaction | null>(null)
  const [bulkDeleting, setBulkDeleting] = useState(false)
  const [detail, setDetail] = useState<CashTransaction | null>(null)

  const changed = () => {
    setSelected(new Set())
    setVersion((v) => v + 1)
  }

  const updateFilters = (patch: Partial<Filters>) => {
    setFilters((prev) => ({ ...prev, ...patch }))
    setSelected(new Set())
    setPage(1)
  }

  const applyPreset = (key: string) => {
    setPreset(key)
    const range = DATE_PRESETS[key]
    if (range) updateFilters(range())
  }

  const clearFilters = () => {
    setPreset('allTime')
    setFilters(EMPTY_FILTERS)
    setSelected(new Set())
    setPage(1)
  }

  const activeFilterCount = [
    filters.accountId, filters.categoryId, filters.payeeId, filters.direction, filters.status, filters.from, filters.to,
  ].filter(Boolean).length

  // Removable chips summarizing what's filtering the ledger while the panel is closed.
  const activeChips: { key: string; label: string; clear: () => void }[] = []
  if (filters.accountId) {
    activeChips.push({
      key: 'account',
      label: `Account: ${accounts.data?.find((a) => a.id === filters.accountId)?.name ?? '…'}`,
      clear: () => updateFilters({ accountId: '' }),
    })
  }
  if (filters.categoryId) {
    activeChips.push({
      key: 'category',
      label: `Category: ${categories.data?.find((c) => c.id === filters.categoryId)?.name ?? '…'}`,
      clear: () => updateFilters({ categoryId: '' }),
    })
  }
  if (filters.payeeId) {
    activeChips.push({
      key: 'payee',
      label: `Payee: ${payees.data?.find((p) => p.id === filters.payeeId)?.name ?? '…'}`,
      clear: () => updateFilters({ payeeId: '' }),
    })
  }
  if (filters.direction) {
    activeChips.push({
      key: 'direction',
      label: filters.direction === 'In' ? 'Money in only' : 'Money out only',
      clear: () => updateFilters({ direction: '' }),
    })
  }
  if (filters.status) {
    activeChips.push({ key: 'status', label: `Status: ${filters.status}`, clear: () => updateFilters({ status: '' }) })
  }
  if (filters.from || filters.to) {
    activeChips.push({
      key: 'dates',
      label: `${filters.from ? formatDate(filters.from) : 'Start'} → ${filters.to ? formatDate(filters.to) : 'today'}`,
      clear: () => { setPreset('allTime'); updateFilters({ from: '', to: '' }) },
    })
  }

  const handleDelete = async () => {
    if (!deleting) return
    try {
      await deleteCashTransaction(deleting.id)
      toast.success(
        deleting.transferGroupId ? 'Transfer deleted' : deleting.splitGroupId ? 'Split deleted' : 'Transaction deleted',
      )
      setDeleting(null)
      changed()
    } catch (err) {
      toast.error(err instanceof Error ? err.message : String(err))
      setDeleting(null)
    }
  }

  const reportBulkResult = (result: BulkTransactionResult, verb: string) => {
    if (result.updatedCount > 0) {
      toast.success(`${result.updatedCount} row${result.updatedCount === 1 ? '' : 's'} ${verb}`)
    }
    if (result.skipped.length > 0) {
      const reasons = [...new Set(result.skipped.map((s) => s.reason))].join('; ')
      toast.error(`${result.skipped.length} skipped — ${reasons}`)
    }
    changed()
  }

  const runBulk = async (action: BulkTransactionAction, categoryId: string | null, status: string | null, verb: string) => {
    try {
      const result = await bulkCashTransactions({ ids: [...selected], action, categoryId, status })
      reportBulkResult(result, verb)
    } catch (err) {
      toast.error(err instanceof Error ? err.message : String(err))
    }
  }

  const toggleSelected = (id: string) => {
    setSelected((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  const pageIds = ledger.data?.items.map((t) => t.id) ?? []
  const allPageSelected = pageIds.length > 0 && pageIds.every((id) => selected.has(id))
  const toggleAll = () => {
    setSelected(allPageSelected ? new Set() : new Set(pageIds))
  }

  const totalCash = accounts.data?.reduce((sum, a) => sum + a.currentBalance, 0) ?? 0
  const totalPages = ledger.data ? Math.max(1, Math.ceil(ledger.data.totalCount / PAGE_SIZE)) : 1
  const showRunningBalance = ledger.data?.items.some((t) => t.runningBalance !== null) ?? false

  return (
    <div>
      <PageHeader
        title="Cash Flow"
        description="Every dollar in and out of the business, across all accounts."
      />

      {/* Account balances */}
      {accounts.data && (
        <div className="mb-6 grid grid-cols-2 gap-4 sm:grid-cols-3 xl:grid-cols-5">
          <BalanceCard label="Total cash" amount={totalCash} highlight />
          {accounts.data.map((a) => (
            <BalanceCard
              key={a.id}
              label={a.name}
              sublabel={CASH_ACCOUNT_TYPE_LABELS[a.type] ?? a.type}
              amount={a.currentBalance}
            />
          ))}
        </div>
      )}

      {/* Toolbar: search + period always visible; the rest lives in a labeled filter panel. */}
      <div className="mb-4 rounded-lg border border-slate-200 bg-white p-4">
        <div className="flex flex-wrap items-end gap-3">
          <LabeledControl label="Search" className="min-w-52 flex-1">
            <input
              type="search"
              placeholder="Description, payee, notes…"
              value={filters.search}
              onChange={(e) => updateFilters({ search: e.target.value })}
              className={`${controlClass} mt-0`}
            />
          </LabeledControl>
          <LabeledControl label="Period">
            <select value={preset} onChange={(e) => applyPreset(e.target.value)} className={`${controlClass} mt-0 w-40`}>
              {preset === '' && <option value="">Custom range</option>}
              <option value="allTime">All time</option>
              <option value="thisMonth">This month</option>
              <option value="lastMonth">Last month</option>
              <option value="thisQuarter">This quarter</option>
              <option value="fyToDate">Financial year to date</option>
              <option value="last12Months">Last 12 months</option>
            </select>
          </LabeledControl>
          <Button variant="secondary" onClick={() => setShowFilters((v) => !v)}>
            {showFilters ? 'Hide filters' : activeFilterCount > 0 ? `Filters · ${activeFilterCount} active` : 'Filters'}
          </Button>
          <div className="ml-auto flex gap-2">
            <Button
              variant="secondary"
              onClick={() => window.location.assign(cashTransactionsExportUrl(query))}
              title="Download everything matching the current filter as a CSV file"
            >
              Export CSV
            </Button>
            <Button variant="secondary" onClick={() => setTransferring(true)}>Transfer</Button>
            <Button variant="secondary" onClick={() => setSplitting({ mode: 'create' })}>Split payment</Button>
            <Button onClick={() => setEditing({ mode: 'create' })}>+ New transaction</Button>
          </div>
        </div>

        {showFilters && (
          <div className="mt-4 grid gap-3 border-t border-slate-100 pt-4 sm:grid-cols-2 lg:grid-cols-4">
            <LabeledControl label="Account">
              <select
                value={filters.accountId}
                onChange={(e) => updateFilters({ accountId: e.target.value })}
                className={`${controlClass} mt-0`}
              >
                <option value="">All accounts</option>
                {accounts.data?.map((a) => (
                  <option key={a.id} value={a.id}>{a.name}</option>
                ))}
              </select>
            </LabeledControl>
            <LabeledControl label="Category">
              <select
                value={filters.categoryId}
                onChange={(e) => updateFilters({ categoryId: e.target.value })}
                className={`${controlClass} mt-0`}
              >
                <option value="">All categories</option>
                {categories.data?.map((c) => (
                  <option key={c.id} value={c.id}>{c.name}</option>
                ))}
              </select>
            </LabeledControl>
            <LabeledControl label="Payee">
              <select
                value={filters.payeeId}
                onChange={(e) => updateFilters({ payeeId: e.target.value })}
                className={`${controlClass} mt-0`}
              >
                <option value="">All payees</option>
                {payees.data?.map((p) => (
                  <option key={p.id} value={p.id}>{p.name}</option>
                ))}
              </select>
            </LabeledControl>
            <LabeledControl label="Money in / out">
              <select
                value={filters.direction}
                onChange={(e) => updateFilters({ direction: e.target.value })}
                className={`${controlClass} mt-0`}
              >
                <option value="">Both</option>
                <option value="In">Money in only</option>
                <option value="Out">Money out only</option>
              </select>
            </LabeledControl>
            <LabeledControl label="Bank status">
              <select
                value={filters.status}
                onChange={(e) => updateFilters({ status: e.target.value })}
                className={`${controlClass} mt-0`}
              >
                <option value="">Any status</option>
                <option value="Pending">Pending — not confirmed yet</option>
                <option value="Cleared">Cleared — money has moved</option>
                <option value="Reconciled">Reconciled — matched to a statement</option>
              </select>
            </LabeledControl>
            <LabeledControl label="From date">
              <input
                type="date"
                value={filters.from}
                onChange={(e) => { setPreset(''); updateFilters({ from: e.target.value }) }}
                className={`${controlClass} mt-0`}
              />
            </LabeledControl>
            <LabeledControl label="To date">
              <input
                type="date"
                value={filters.to}
                onChange={(e) => { setPreset(''); updateFilters({ to: e.target.value }) }}
                className={`${controlClass} mt-0`}
              />
            </LabeledControl>
            <div className="flex items-end">
              <Button variant="ghost" onClick={clearFilters} disabled={activeFilterCount === 0 && !filters.search}>
                Clear all filters
              </Button>
            </div>
          </div>
        )}

        {!showFilters && activeChips.length > 0 && (
          <div className="mt-3 flex flex-wrap items-center gap-2">
            {activeChips.map((chip) => (
              <span
                key={chip.key}
                className="inline-flex items-center gap-1.5 rounded-full border border-slate-200 bg-slate-50 px-2.5 py-1 text-xs text-slate-600"
              >
                {chip.label}
                <button
                  type="button"
                  onClick={chip.clear}
                  className="text-slate-400 hover:text-slate-700"
                  aria-label={`Remove filter ${chip.label}`}
                >
                  ✕
                </button>
              </span>
            ))}
            <button type="button" onClick={clearFilters} className="text-xs text-slate-400 underline hover:text-slate-600">
              Clear all
            </button>
          </div>
        )}
      </div>

      {/* Filter-wide totals */}
      {ledger.data && (
        <div className="mb-4 flex flex-wrap gap-6 rounded-lg border border-slate-200 bg-white px-4 py-3 text-sm">
          <span className="text-slate-500">
            Cash in <strong className="ml-1 text-emerald-600">{currency(ledger.data.totalIn)}</strong>
          </span>
          <span className="text-slate-500">
            Cash out <strong className="ml-1 text-red-600">{currency(ledger.data.totalOut)}</strong>
          </span>
          <span className="text-slate-500">
            Net{' '}
            <strong className={`ml-1 ${ledger.data.totalIn - ledger.data.totalOut >= 0 ? 'text-emerald-600' : 'text-red-600'}`}>
              {currency(ledger.data.totalIn - ledger.data.totalOut)}
            </strong>
          </span>
          <span className="ml-auto text-slate-400">
            {ledger.data.totalCount} transaction{ledger.data.totalCount === 1 ? '' : 's'} · transfers excluded from totals
          </span>
        </div>
      )}

      {/* Bulk action bar */}
      {selected.size > 0 && (
        <BulkBar
          count={selected.size}
          categories={categories.data ?? []}
          onSetCategory={(categoryId) => void runBulk('SetCategory', categoryId, null, 'recategorized')}
          onSetStatus={(status) => void runBulk('SetStatus', null, status, status === 'Cleared' ? 'marked cleared' : 'marked pending')}
          onDelete={() => setBulkDeleting(true)}
          onClear={() => setSelected(new Set())}
        />
      )}

      {ledger.loading && <StateMessage title="Loading transactions…" />}
      {ledger.error && <StateMessage title="Could not load transactions" description={ledger.error.message} />}
      {ledger.data && ledger.data.items.length === 0 && (
        <StateMessage
          title="No transactions"
          description="Record a transaction, or mark an invoice paid once payment routing is set up under Cash Accounts."
        />
      )}

      {ledger.data && ledger.data.items.length > 0 && (
        <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white">
          <table className="min-w-full divide-y divide-slate-200 text-sm">
            <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
              <tr>
                <th className="w-8 px-3 py-3">
                  <input
                    type="checkbox"
                    checked={allPageSelected}
                    onChange={toggleAll}
                    aria-label="Select all rows on this page"
                  />
                </th>
                <th className="px-4 py-3">Date</th>
                <th className="px-4 py-3">Description</th>
                <th className="px-4 py-3">Category</th>
                <th className="px-4 py-3">Account</th>
                <th className="px-4 py-3">Payee / Payer</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3 text-right">Amount</th>
                {showRunningBalance && <th className="px-4 py-3 text-right">Balance</th>}
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {ledger.data.items.map((t) => (
                <tr key={t.id} className={`hover:bg-slate-50 ${selected.has(t.id) ? 'bg-slate-50' : ''}`}>
                  <td className="px-3 py-3">
                    <input
                      type="checkbox"
                      checked={selected.has(t.id)}
                      onChange={() => toggleSelected(t.id)}
                      aria-label={`Select ${t.description}`}
                    />
                  </td>
                  <td className="whitespace-nowrap px-4 py-3 text-slate-600">{formatDate(t.date)}</td>
                  <td className="px-4 py-3 font-medium text-slate-900">
                    {t.description}
                    {t.invoiceId && <RowTag>invoice</RowTag>}
                    {t.transferGroupId && <RowTag>transfer</RowTag>}
                    {t.splitGroupId && <RowTag>split</RowTag>}
                    {t.attachments.length > 0 && <RowTag>📎 {t.attachments.length}</RowTag>}
                  </td>
                  <td className="px-4 py-3 text-slate-600">{t.categoryName}</td>
                  <td className="px-4 py-3 text-slate-600">{t.accountName}</td>
                  <td className="px-4 py-3 text-slate-600">{orDash(t.counterparty)}</td>
                  <td className="whitespace-nowrap px-4 py-3"><StatusBadge status={t.status} /></td>
                  <td
                    className={`whitespace-nowrap px-4 py-3 text-right font-medium ${
                      t.direction === 'In' ? 'text-emerald-600' : 'text-red-600'
                    }`}
                  >
                    {t.direction === 'In' ? '+' : '−'}{currency(t.amount)}
                  </td>
                  {showRunningBalance && (
                    <td className="whitespace-nowrap px-4 py-3 text-right text-slate-600">
                      {t.runningBalance !== null ? currency(t.runningBalance) : ''}
                    </td>
                  )}
                  <td className="whitespace-nowrap px-4 py-3 text-right">
                    <Button variant="ghost" size="sm" onClick={() => setDetail(t)}>
                      Details
                    </Button>
                    {/* Invoice rows are corrected from the invoice; transfer legs are recreated, not edited. */}
                    {!t.invoiceId && !t.transferGroupId && !t.splitGroupId && t.status !== 'Reconciled' && (
                      <Button variant="ghost" size="sm" onClick={() => setEditing({ mode: 'edit', row: t })}>
                        Edit
                      </Button>
                    )}
                    {t.splitGroupId && t.status !== 'Reconciled' && (
                      <Button variant="ghost" size="sm" onClick={() => setSplitting({ mode: 'edit', groupId: t.splitGroupId! })}>
                        Edit split
                      </Button>
                    )}
                    {!t.invoiceId && t.status !== 'Reconciled' && (
                      <Button variant="ghost" size="sm" className="text-red-600" onClick={() => setDeleting(t)}>
                        Delete
                      </Button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="flex items-center justify-between border-t border-slate-100 px-4 py-3 text-sm text-slate-500">
            <span>Page {page} of {totalPages}</span>
            <div className="flex gap-2">
              <Button variant="secondary" size="sm" disabled={page <= 1} onClick={() => { setSelected(new Set()); setPage((p) => p - 1) }}>
                ← Previous
              </Button>
              <Button variant="secondary" size="sm" disabled={page >= totalPages} onClick={() => { setSelected(new Set()); setPage((p) => p + 1) }}>
                Next →
              </Button>
            </div>
          </div>
        </div>
      )}

      <Modal
        open={editing !== null}
        title={editing?.mode === 'edit' ? 'Edit Transaction' : 'Record Transaction'}
        onClose={() => setEditing(null)}
      >
        {editing && accounts.data && categories.data && (
          <TransactionForm
            initial={editing.mode === 'edit' ? editing.row : null}
            accounts={accounts.data}
            categories={categories.data}
            payees={payees.data ?? []}
            onCancel={() => setEditing(null)}
            onSubmit={async (body) => {
              if (editing.mode === 'edit') {
                await updateCashTransaction(editing.row.id, body)
                toast.success('Transaction updated')
              } else {
                await createCashTransaction(body)
                toast.success('Transaction recorded')
              }
              setEditing(null)
              changed()
            }}
          />
        )}
      </Modal>

      <Modal open={transferring} title="Transfer Between Accounts" onClose={() => setTransferring(false)}>
        {transferring && accounts.data && (
          <TransferForm
            accounts={accounts.data}
            onCancel={() => setTransferring(false)}
            onSubmit={async (body) => {
              await createTransfer(body)
              toast.success('Transfer recorded')
              setTransferring(false)
              changed()
            }}
          />
        )}
      </Modal>

      <Modal
        open={splitting !== null}
        title={splitting?.mode === 'edit' ? 'Edit Split Transaction' : 'Split Transaction'}
        onClose={() => setSplitting(null)}
      >
        {splitting && accounts.data && categories.data && (
          <SplitForm
            groupId={splitting.mode === 'edit' ? splitting.groupId : null}
            accounts={accounts.data}
            categories={categories.data}
            payees={payees.data ?? []}
            onCancel={() => setSplitting(null)}
            onSubmit={async (groupId, body) => {
              if (groupId) {
                await updateSplitTransaction(groupId, body)
                toast.success('Split updated')
              } else {
                await createSplitTransaction(body)
                toast.success('Split recorded')
              }
              setSplitting(null)
              changed()
            }}
          />
        )}
      </Modal>

      <TransactionDetailModal
        transaction={detail}
        onClose={() => setDetail(null)}
        onChanged={async (id) => {
          setDetail(await getCashTransaction(id))
          changed()
        }}
      />

      <ConfirmDialog
        open={deleting !== null}
        title={deleting?.transferGroupId ? 'Delete Transfer' : deleting?.splitGroupId ? 'Delete Split' : 'Delete Transaction'}
        message={
          deleting?.transferGroupId
            ? 'This removes both legs of the transfer from the ledger. This cannot be undone.'
            : deleting?.splitGroupId
              ? 'This removes every line of the split from the ledger. This cannot be undone.'
              : deleting
                ? `Delete “${deleting.description}”? This cannot be undone.`
                : ''
        }
        onConfirm={handleDelete}
        onCancel={() => setDeleting(null)}
      />

      <ConfirmDialog
        open={bulkDeleting}
        title="Delete Selected Transactions"
        message={`Delete ${selected.size} selected transaction${selected.size === 1 ? '' : 's'}? Protected rows (invoice-posted, transfer legs, split lines, reconciled, locked) are skipped automatically. This cannot be undone.`}
        onConfirm={async () => {
          setBulkDeleting(false)
          await runBulk('Delete', null, null, 'deleted')
        }}
        onCancel={() => setBulkDeleting(false)}
      />
    </div>
  )
}

function BalanceCard({
  label,
  sublabel,
  amount,
  highlight,
}: {
  label: string
  sublabel?: string
  amount: number
  highlight?: boolean
}) {
  return (
    <div
      className={`rounded-lg border px-4 py-3 ${
        highlight ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white'
      }`}
    >
      <p className={`truncate text-xs font-medium uppercase tracking-wide ${highlight ? 'text-slate-300' : 'text-slate-500'}`}>
        {label}
      </p>
      {sublabel && <p className="truncate text-xs text-slate-400">{sublabel}</p>}
      <p className={`mt-1 text-lg font-semibold ${highlight ? '' : amount < 0 ? 'text-red-600' : 'text-slate-900'}`}>
        {currency(amount)}
      </p>
    </div>
  )
}

/** A form control with a small always-visible label — no more mystery dropdowns. */
function LabeledControl({
  label,
  children,
  className = '',
}: {
  label: string
  children: React.ReactNode
  className?: string
}) {
  return (
    <label className={`block ${className}`}>
      <span className="mb-1 block text-xs font-medium text-slate-500">{label}</span>
      {children}
    </label>
  )
}

function RowTag({ children }: { children: React.ReactNode }) {
  return (
    <span className="ml-2 inline-block rounded bg-slate-100 px-1.5 py-0.5 text-xs font-normal text-slate-500">
      {children}
    </span>
  )
}

function StatusBadge({ status }: { status: string }) {
  const styles: Record<string, string> = {
    Pending: 'bg-amber-50 text-amber-700 border-amber-200',
    Cleared: 'bg-slate-50 text-slate-500 border-slate-200',
    Reconciled: 'bg-sky-50 text-sky-700 border-sky-200',
  }
  return (
    <span className={`inline-block rounded border px-1.5 py-0.5 text-xs ${styles[status] ?? styles.Cleared}`}>
      {status}
    </span>
  )
}

// --- Bulk action bar ---------------------------------------------------------------

function BulkBar({
  count,
  categories,
  onSetCategory,
  onSetStatus,
  onDelete,
  onClear,
}: {
  count: number
  categories: TransactionCategory[]
  onSetCategory: (categoryId: string) => void
  onSetStatus: (status: string) => void
  onDelete: () => void
  onClear: () => void
}) {
  const [categoryId, setCategoryId] = useState('')

  return (
    <div className="mb-4 flex flex-wrap items-center gap-3 rounded-lg border border-slate-900 bg-slate-900 px-4 py-2.5 text-sm text-white">
      <span className="font-medium">{count} selected</span>
      <span className="text-slate-500">·</span>
      <select
        value={categoryId}
        onChange={(e) => setCategoryId(e.target.value)}
        className="rounded-md border border-slate-600 bg-slate-800 px-2 py-1.5 text-sm text-white"
        aria-label="Bulk category"
      >
        <option value="">Set category…</option>
        {categories.filter((c) => !c.isArchived).map((c) => (
          <option key={c.id} value={c.id}>{c.name}</option>
        ))}
      </select>
      <Button size="sm" variant="secondary" disabled={!categoryId} onClick={() => categoryId && onSetCategory(categoryId)}>
        Apply
      </Button>
      <span className="text-slate-500">·</span>
      <Button size="sm" variant="secondary" onClick={() => onSetStatus('Cleared')}>Mark cleared</Button>
      <Button size="sm" variant="secondary" onClick={() => onSetStatus('Pending')}>Mark pending</Button>
      <span className="text-slate-500">·</span>
      <Button size="sm" variant="secondary" className="text-red-300" onClick={onDelete}>Delete</Button>
      <button type="button" onClick={onClear} className="ml-auto text-slate-300 hover:text-white">
        Clear selection
      </button>
    </div>
  )
}

// --- Record / edit form --------------------------------------------------------

function TransactionForm({
  initial,
  accounts,
  categories,
  payees,
  onSubmit,
  onCancel,
}: {
  initial: CashTransaction | null
  accounts: CashAccount[]
  categories: TransactionCategory[]
  payees: Payee[]
  onSubmit: (body: CashTransactionRequest) => Promise<void>
  onCancel: () => void
}) {
  const [accountId, setAccountId] = useState(initial?.accountId ?? accounts[0]?.id ?? '')
  const [direction, setDirection] = useState(initial?.direction ?? 'Out')
  const [amount, setAmount] = useState(initial ? String(initial.amount) : '')
  const [date, setDate] = useState(initial?.date ?? today())
  const [description, setDescription] = useState(initial?.description ?? '')
  const [categoryId, setCategoryId] = useState(initial?.categoryId ?? '')
  const [payeeId, setPayeeId] = useState(initial?.payeeId ?? '')
  const [counterparty, setCounterparty] = useState(initial?.counterparty ?? '')
  const [status, setStatus] = useState(initial?.status ?? 'Cleared')
  const [gstTreatment, setGstTreatment] = useState(initial?.gstTreatment ?? '')
  const [notes, setNotes] = useState(initial?.notes ?? '')
  const [suggestionHint, setSuggestionHint] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const visibleCategories = categories.filter(
    (c) => !c.isArchived && (c.direction === 'Either' || c.direction === direction),
  )

  const changeDirection = (next: string) => {
    setDirection(next)
    // Reset a category that no longer applies to the new direction.
    const current = categories.find((c) => c.id === categoryId)
    if (current && current.direction !== 'Either' && current.direction !== next) {
      setCategoryId('')
    }
  }

  const choosePayee = (id: string) => {
    setPayeeId(id)
    const payee = payees.find((p) => p.id === id)
    if (!payee) return
    // The payee's defaults pre-fill what the user hasn't chosen yet.
    if (payee.defaultCategoryId && !categoryId) {
      const candidate = categories.find((c) => c.id === payee.defaultCategoryId)
      if (candidate && (candidate.direction === 'Either' || candidate.direction === direction)) {
        setCategoryId(payee.defaultCategoryId)
      }
    }
    if (payee.defaultGstTreatment && !gstTreatment) setGstTreatment(payee.defaultGstTreatment)
  }

  // Rule-driven suggestion when the description is settled and no category is picked yet.
  const suggest = async () => {
    if (categoryId || !description.trim()) return
    try {
      const suggestion = await suggestCategorization({
        description: description.trim(),
        counterparty: counterparty.trim() || null,
        direction,
        amount: amount ? Number(amount) : null,
      })
      if (!suggestion) return
      const candidate = categories.find((c) => c.id === suggestion.categoryId)
      if (candidate && (candidate.direction === 'Either' || candidate.direction === direction)) {
        setCategoryId(suggestion.categoryId)
        if (suggestion.gstTreatment && !gstTreatment) setGstTreatment(suggestion.gstTreatment)
        if (suggestion.payeeId && !payeeId) setPayeeId(suggestion.payeeId)
        setSuggestionHint(`Filled in by rule “${suggestion.ruleName}”`)
      }
    } catch {
      // Suggestions are best-effort; a failed lookup should never block data entry.
    }
  }

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)
    setBusy(true)
    try {
      await onSubmit({
        accountId,
        direction,
        amount: Number(amount),
        date,
        description: description.trim(),
        categoryId,
        payeeId: payeeId || null,
        counterparty: counterparty.trim() || null,
        gstTreatment: gstTreatment || null,
        status: status || null,
        notes: notes.trim() || null,
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
        <Field label="Account" required>
          <select required value={accountId} onChange={(e) => setAccountId(e.target.value)} className={controlClass}>
            {accounts.map((a) => (
              <option key={a.id} value={a.id}>{a.name}</option>
            ))}
          </select>
        </Field>
        <Field label="Date" required>
          <input type="date" required value={date} onChange={(e) => setDate(e.target.value)} className={controlClass} />
        </Field>
      </div>
      <Field label="Description" required>
        <input
          type="text" required maxLength={500} value={description}
          onChange={(e) => setDescription(e.target.value)} onBlur={() => void suggest()} className={controlClass}
        />
      </Field>
      {suggestionHint && <p className="text-xs text-sky-600">{suggestionHint}</p>}
      <div className="grid grid-cols-2 gap-4">
        <Field label="Category" required>
          <select required value={categoryId} onChange={(e) => setCategoryId(e.target.value)} className={controlClass}>
            <option value="" disabled>Select a category…</option>
            {visibleCategories.map((c) => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </select>
        </Field>
        <Field label="GST treatment">
          <select value={gstTreatment} onChange={(e) => setGstTreatment(e.target.value)} className={controlClass}>
            <option value="">Category default</option>
            {GST_OPTIONS.map((g) => (
              <option key={g} value={g}>{g === 'ZeroRated' ? 'Zero-rated' : g}</option>
            ))}
          </select>
        </Field>
        <Field label="Payee">
          <select value={payeeId} onChange={(e) => choosePayee(e.target.value)} className={controlClass}>
            <option value="">— none —</option>
            {payees.map((p) => (
              <option key={p.id} value={p.id}>{p.name}</option>
            ))}
          </select>
        </Field>
        <Field label="Status">
          <select value={status} onChange={(e) => setStatus(e.target.value)} className={controlClass}>
            <option value="Cleared">Cleared — money has moved</option>
            <option value="Pending">Pending — not confirmed yet</option>
          </select>
        </Field>
      </div>
      {!payeeId && (
        <Field label="Payee / payer (free text)">
          <input type="text" maxLength={200} value={counterparty} onChange={(e) => setCounterparty(e.target.value)} className={controlClass} />
        </Field>
      )}
      <Field label="Notes">
        <textarea rows={2} maxLength={2000} value={notes} onChange={(e) => setNotes(e.target.value)} className={controlClass} />
      </Field>
      {error && <p className="text-sm text-red-600">{error}</p>}
      <div className="flex justify-end gap-2 pt-1">
        <Button type="button" variant="secondary" onClick={onCancel} disabled={busy}>Cancel</Button>
        <Button type="submit" disabled={busy}>
          {busy ? 'Saving…' : initial ? 'Save changes' : 'Record transaction'}
        </Button>
      </div>
    </form>
  )
}

// --- Transfer form ---------------------------------------------------------------

function TransferForm({
  accounts,
  onSubmit,
  onCancel,
}: {
  accounts: CashAccount[]
  onSubmit: (body: {
    fromAccountId: string
    toAccountId: string
    amount: number
    date: string
    description: string | null
    notes: string | null
  }) => Promise<void>
  onCancel: () => void
}) {
  const [fromAccountId, setFromAccountId] = useState(accounts[0]?.id ?? '')
  const [toAccountId, setToAccountId] = useState(accounts[1]?.id ?? '')
  const [amount, setAmount] = useState('')
  const [date, setDate] = useState(today())
  const [description, setDescription] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)
    setBusy(true)
    try {
      await onSubmit({
        fromAccountId,
        toAccountId,
        amount: Number(amount),
        date,
        description: description.trim() || null,
        notes: null,
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
        <Field label="From account" required>
          <select required value={fromAccountId} onChange={(e) => setFromAccountId(e.target.value)} className={controlClass}>
            {accounts.map((a) => (
              <option key={a.id} value={a.id}>{a.name}</option>
            ))}
          </select>
        </Field>
        <Field label="To account" required>
          <select required value={toAccountId} onChange={(e) => setToAccountId(e.target.value)} className={controlClass}>
            {accounts.map((a) => (
              <option key={a.id} value={a.id}>{a.name}</option>
            ))}
          </select>
        </Field>
        <Field label="Amount" required>
          <input type="number" step="0.01" min="0.01" required value={amount} onChange={(e) => setAmount(e.target.value)} className={controlClass} />
        </Field>
        <Field label="Date" required>
          <input type="date" required value={date} onChange={(e) => setDate(e.target.value)} className={controlClass} />
        </Field>
      </div>
      <Field label="Description">
        <input
          type="text" maxLength={500} value={description} placeholder="Defaults to “Transfer to/from …”"
          onChange={(e) => setDescription(e.target.value)} className={controlClass}
        />
      </Field>
      {error && <p className="text-sm text-red-600">{error}</p>}
      <div className="flex justify-end gap-2 pt-1">
        <Button type="button" variant="secondary" onClick={onCancel} disabled={busy}>Cancel</Button>
        <Button type="submit" disabled={busy}>{busy ? 'Saving…' : 'Transfer'}</Button>
      </div>
    </form>
  )
}

// --- Split form ---------------------------------------------------------------------

interface SplitLineDraft {
  amount: string
  categoryId: string
  description: string
}

function SplitForm({
  groupId,
  accounts,
  categories,
  payees,
  onSubmit,
  onCancel,
}: {
  /** Null = new split; set = replace this group's lines. */
  groupId: string | null
  accounts: CashAccount[]
  categories: TransactionCategory[]
  payees: Payee[]
  onSubmit: (groupId: string | null, body: SplitTransactionRequest) => Promise<void>
  onCancel: () => void
}) {
  const [accountId, setAccountId] = useState(accounts[0]?.id ?? '')
  const [direction, setDirection] = useState('Out')
  const [date, setDate] = useState(today())
  const [description, setDescription] = useState('')
  const [payeeId, setPayeeId] = useState('')
  const [counterparty, setCounterparty] = useState('')
  const [notes, setNotes] = useState('')
  const [lines, setLines] = useState<SplitLineDraft[]>([
    { amount: '', categoryId: '', description: '' },
    { amount: '', categoryId: '', description: '' },
  ])
  const [loadedGroup, setLoadedGroup] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  // Existing group rows seed the form once when editing.
  const existing = useAsync(
    () => (groupId ? getCashTransactions({ splitGroupId: groupId, pageSize: 100 }) : Promise.resolve(null)),
    [groupId],
  )
  if (groupId && existing.data && !loadedGroup) {
    const rows = existing.data.items
    if (rows.length > 0) {
      const first = rows[rows.length - 1] // list is newest-first; any row carries the shared fields
      setAccountId(first.accountId)
      setDirection(first.direction)
      setDate(first.date)
      setDescription(first.description)
      setPayeeId(first.payeeId ?? '')
      setCounterparty(first.payeeId ? '' : (first.counterparty ?? ''))
      setNotes(first.notes ?? '')
      setLines(rows.map((r) => ({ amount: String(r.amount), categoryId: r.categoryId, description: '' })).reverse())
    }
    setLoadedGroup(true)
  }

  const visibleCategories = categories.filter(
    (c) => !c.isArchived && (c.direction === 'Either' || c.direction === direction),
  )

  const updateLine = (index: number, patch: Partial<SplitLineDraft>) => {
    setLines((prev) => prev.map((l, i) => (i === index ? { ...l, ...patch } : l)))
  }

  const total = lines.reduce((sum, l) => sum + (Number(l.amount) || 0), 0)

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)
    setBusy(true)
    try {
      await onSubmit(groupId, {
        accountId,
        direction,
        date,
        description: description.trim(),
        payeeId: payeeId || null,
        counterparty: counterparty.trim() || null,
        status: null,
        notes: notes.trim() || null,
        lines: lines.map((l) => ({
          amount: Number(l.amount),
          categoryId: l.categoryId,
          gstTreatment: null,
          description: l.description.trim() || null,
        })),
      })
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <p className="text-sm text-slate-500">
        One payment covering several categories — each line lands in the ledger as its own row, grouped together.
      </p>
      <div className="grid grid-cols-2 gap-4">
        <Field label="Direction" required>
          <select value={direction} onChange={(e) => setDirection(e.target.value)} className={controlClass}>
            <option value="Out">Money out</option>
            <option value="In">Money in</option>
          </select>
        </Field>
        <Field label="Account" required>
          <select required value={accountId} onChange={(e) => setAccountId(e.target.value)} className={controlClass}>
            {accounts.map((a) => (
              <option key={a.id} value={a.id}>{a.name}</option>
            ))}
          </select>
        </Field>
        <Field label="Date" required>
          <input type="date" required value={date} onChange={(e) => setDate(e.target.value)} className={controlClass} />
        </Field>
        <Field label="Payee">
          <select value={payeeId} onChange={(e) => setPayeeId(e.target.value)} className={controlClass}>
            <option value="">— none —</option>
            {payees.map((p) => (
              <option key={p.id} value={p.id}>{p.name}</option>
            ))}
          </select>
        </Field>
      </div>
      <Field label="Description" required>
        <input type="text" required maxLength={500} value={description} onChange={(e) => setDescription(e.target.value)} className={controlClass} />
      </Field>

      <div>
        <p className="mb-2 text-sm font-medium text-slate-700">Lines</p>
        <div className="space-y-2">
          {lines.map((line, i) => (
            <div key={i} className="flex items-start gap-2">
              <input
                type="number" step="0.01" min="0.01" required placeholder="Amount" value={line.amount}
                onChange={(e) => updateLine(i, { amount: e.target.value })}
                className={`${controlClass} mt-0 w-28`} aria-label={`Line ${i + 1} amount`}
              />
              <select
                required value={line.categoryId}
                onChange={(e) => updateLine(i, { categoryId: e.target.value })}
                className={`${controlClass} mt-0 flex-1`} aria-label={`Line ${i + 1} category`}
              >
                <option value="" disabled>Category…</option>
                {visibleCategories.map((c) => (
                  <option key={c.id} value={c.id}>{c.name}</option>
                ))}
              </select>
              <input
                type="text" maxLength={500} placeholder="Line description (optional)" value={line.description}
                onChange={(e) => updateLine(i, { description: e.target.value })}
                className={`${controlClass} mt-0 flex-1`} aria-label={`Line ${i + 1} description`}
              />
              <Button
                type="button" variant="ghost" size="sm" className="mt-1 text-red-600"
                disabled={lines.length <= 2}
                onClick={() => setLines((prev) => prev.filter((_, idx) => idx !== i))}
              >
                ✕
              </Button>
            </div>
          ))}
        </div>
        <div className="mt-2 flex items-center justify-between text-sm">
          <Button type="button" variant="secondary" size="sm" onClick={() => setLines((prev) => [...prev, { amount: '', categoryId: '', description: '' }])}>
            + Add line
          </Button>
          <span className="text-slate-500">Total <strong className="text-slate-900">{currency(total)}</strong></span>
        </div>
      </div>

      <Field label="Notes">
        <textarea rows={2} maxLength={2000} value={notes} onChange={(e) => setNotes(e.target.value)} className={controlClass} />
      </Field>
      {groupId && (
        <p className="rounded-md bg-slate-50 px-3 py-2 text-xs text-slate-500">
          Saving replaces every line of this split; attachments on replaced lines are removed.
        </p>
      )}
      {error && <p className="text-sm text-red-600">{error}</p>}
      <div className="flex justify-end gap-2 pt-1">
        <Button type="button" variant="secondary" onClick={onCancel} disabled={busy}>Cancel</Button>
        <Button type="submit" disabled={busy}>{busy ? 'Saving…' : groupId ? 'Save split' : 'Record split'}</Button>
      </div>
    </form>
  )
}

// --- Detail modal with attachments & history ---------------------------------------

function TransactionDetailModal({
  transaction,
  onClose,
  onChanged,
}: {
  transaction: CashTransaction | null
  onClose: () => void
  onChanged: (id: string) => Promise<void>
}) {
  const toast = useToast()
  const [busy, setBusy] = useState(false)
  const [showHistory, setShowHistory] = useState(false)

  const upload = async (file: File | undefined) => {
    if (!transaction || !file) return
    setBusy(true)
    try {
      await addTransactionAttachment(transaction.id, file)
      toast.success('Attachment uploaded')
      await onChanged(transaction.id)
    } catch (err) {
      toast.error(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  const remove = async (attachmentId: string) => {
    if (!transaction) return
    setBusy(true)
    try {
      await deleteTransactionAttachment(transaction.id, attachmentId)
      toast.success('Attachment removed')
      await onChanged(transaction.id)
    } catch (err) {
      toast.error(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal open={transaction !== null} title="Transaction Details" onClose={() => { setShowHistory(false); onClose() }}>
      {transaction && (
        <div className="space-y-4 text-sm">
          <div className="grid grid-cols-2 gap-x-4 gap-y-2">
            <DetailRow label="Date" value={formatDate(transaction.date)} />
            <DetailRow
              label="Amount"
              value={`${transaction.direction === 'In' ? '+' : '−'}${currency(transaction.amount)}`}
            />
            <DetailRow label="Account" value={transaction.accountName} />
            <DetailRow label="Category" value={transaction.categoryName} />
            <DetailRow label="Payee / payer" value={orDash(transaction.counterparty)} />
            <DetailRow label="GST treatment" value={transaction.gstTreatment} />
            <DetailRow label="Status" value={transaction.status} />
          </div>
          <DetailRow label="Description" value={transaction.description} />
          {transaction.notes && <DetailRow label="Notes" value={transaction.notes} />}
          {transaction.invoiceId && (
            <p className="flex items-center justify-between rounded-md bg-slate-50 px-3 py-2 text-xs text-slate-500">
              <span>Posted automatically from an invoice payment — correct it from the invoice, not here.</span>
              {transaction.jobId && (
                <Link to={`/jobs/${transaction.jobId}`} className="ml-3 shrink-0 font-medium text-slate-700 hover:text-slate-900 hover:underline">
                  View job →
                </Link>
              )}
            </p>
          )}
          {transaction.transferGroupId && (
            <p className="rounded-md bg-slate-50 px-3 py-2 text-xs text-slate-500">
              One leg of an account-to-account transfer; deleting it removes both legs.
            </p>
          )}
          {transaction.splitGroupId && (
            <p className="rounded-md bg-slate-50 px-3 py-2 text-xs text-slate-500">
              One line of a split payment; the split is edited and deleted as a whole.
            </p>
          )}
          {transaction.status === 'Reconciled' && (
            <p className="rounded-md bg-sky-50 px-3 py-2 text-xs text-sky-700">
              Reconciled against a bank statement — this row is locked; reverse and re-enter to correct it.
            </p>
          )}

          <div>
            <div className="mb-2 flex items-center justify-between">
              <h3 className="font-medium text-slate-900">Receipts & documents</h3>
              <label className="cursor-pointer text-sm font-medium text-slate-600 hover:text-slate-900">
                {busy ? 'Working…' : '+ Upload file'}
                <input
                  type="file"
                  className="hidden"
                  disabled={busy}
                  onChange={(e) => {
                    void upload(e.target.files?.[0])
                    e.target.value = ''
                  }}
                />
              </label>
            </div>
            {transaction.attachments.length === 0 ? (
              <p className="text-slate-400">No files attached.</p>
            ) : (
              <ul className="divide-y divide-slate-100 rounded-md border border-slate-200">
                {transaction.attachments.map((a) => (
                  <li key={a.id} className="flex items-center justify-between gap-3 px-3 py-2">
                    <a
                      href={transactionAttachmentUrl(transaction.id, a.id)}
                      target="_blank"
                      rel="noreferrer"
                      className="truncate font-medium text-slate-700 hover:text-slate-900 hover:underline"
                    >
                      📎 {a.fileName}
                    </a>
                    <span className="shrink-0 text-xs text-slate-400">{Math.max(1, Math.round(a.sizeBytes / 1024))} KB</span>
                    <Button variant="ghost" size="sm" className="text-red-600" disabled={busy} onClick={() => void remove(a.id)}>
                      Remove
                    </Button>
                  </li>
                ))}
              </ul>
            )}
          </div>

          <div>
            <button
              type="button"
              onClick={() => setShowHistory((v) => !v)}
              className="text-sm font-medium text-slate-600 hover:text-slate-900"
            >
              {showHistory ? '▾ History' : '▸ History'}
            </button>
            {showHistory && <AuditHistory transaction={transaction} />}
          </div>
        </div>
      )}
    </Modal>
  )
}

/** The audit trail for a row — including its transfer/split group's entries when it has one. */
function AuditHistory({ transaction }: { transaction: CashTransaction }) {
  const trail = useAsync(async () => {
    const own = await getAuditTrail({ entityId: transaction.id, pageSize: 50 })
    const groupId = transaction.transferGroupId ?? transaction.splitGroupId
    const group = groupId ? await getAuditTrail({ entityId: groupId, pageSize: 50 }) : null
    return [...own.items, ...(group?.items ?? [])].sort((a, b) => b.timestampUtc.localeCompare(a.timestampUtc))
  }, [transaction.id])

  if (trail.loading) return <p className="mt-2 text-xs text-slate-400">Loading history…</p>
  if (trail.error) return <p className="mt-2 text-xs text-red-600">Could not load history.</p>
  if (!trail.data || trail.data.length === 0) {
    return <p className="mt-2 text-xs text-slate-400">No recorded changes (rows created before the audit trail shipped have none).</p>
  }

  return (
    <ul className="mt-2 space-y-2 border-l-2 border-slate-100 pl-3">
      {trail.data.map((entry: CashFlowAuditLog) => (
        <li key={entry.id} className="text-xs">
          <span className="font-medium text-slate-700">{entry.action}</span>
          <span className="ml-2 text-slate-400">{formatDate(entry.timestampUtc)}</span>
          <p className="text-slate-600">{entry.summary}</p>
        </li>
      ))}
    </ul>
  )
}

function DetailRow({ label, value }: { label: string; value: string }) {
  return (
    <p>
      <span className="block text-xs font-medium uppercase tracking-wide text-slate-400">{label}</span>
      <span className="text-slate-800">{value}</span>
    </p>
  )
}
