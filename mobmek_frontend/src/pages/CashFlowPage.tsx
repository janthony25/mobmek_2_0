import { useState } from 'react'
import type { FormEvent } from 'react'
import { getCashAccounts } from '@/api/cashAccounts'
import { getTransactionCategories } from '@/api/transactionCategories'
import {
  addTransactionAttachment,
  createCashTransaction,
  createTransfer,
  deleteCashTransaction,
  deleteTransactionAttachment,
  getCashTransaction,
  getCashTransactions,
  transactionAttachmentUrl,
  updateCashTransaction,
} from '@/api/cashTransactions'
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
  CashAccount,
  CashTransaction,
  CashTransactionRequest,
  TransactionCategory,
} from '@/types'
import { CASH_ACCOUNT_TYPE_LABELS } from '@/types'

const PAGE_SIZE = 25

const GST_OPTIONS = ['Taxable', 'Exempt', 'ZeroRated'] as const

const today = () => new Date().toISOString().slice(0, 10)

interface Filters {
  accountId: string
  categoryId: string
  direction: string
  from: string
  to: string
  search: string
}

const EMPTY_FILTERS: Filters = { accountId: '', categoryId: '', direction: '', from: '', to: '', search: '' }

export function CashFlowPage() {
  const toast = useToast()
  const [filters, setFilters] = useState<Filters>(EMPTY_FILTERS)
  const [page, setPage] = useState(1)
  // Bumped after any mutation so the ledger, totals and account balances refresh together.
  const [version, setVersion] = useState(0)

  const accounts = useAsync(() => getCashAccounts(), [version])
  const categories = useAsync(() => getTransactionCategories(), [version])
  const ledger = useAsync(
    () =>
      getCashTransactions({
        accountId: filters.accountId || undefined,
        categoryId: filters.categoryId || undefined,
        direction: filters.direction || undefined,
        from: filters.from || undefined,
        to: filters.to || undefined,
        search: filters.search || undefined,
        page,
        pageSize: PAGE_SIZE,
      }),
    [filters, page, version],
  )

  const [editing, setEditing] = useState<{ mode: 'create' } | { mode: 'edit'; row: CashTransaction } | null>(null)
  const [transferring, setTransferring] = useState(false)
  const [deleting, setDeleting] = useState<CashTransaction | null>(null)
  const [detail, setDetail] = useState<CashTransaction | null>(null)

  const changed = () => setVersion((v) => v + 1)

  const updateFilters = (patch: Partial<Filters>) => {
    setFilters((prev) => ({ ...prev, ...patch }))
    setPage(1)
  }

  const handleDelete = async () => {
    if (!deleting) return
    await deleteCashTransaction(deleting.id)
    toast.success(deleting.transferGroupId ? 'Transfer deleted' : 'Transaction deleted')
    setDeleting(null)
    changed()
  }

  const totalCash = accounts.data?.reduce((sum, a) => sum + a.currentBalance, 0) ?? 0
  const totalPages = ledger.data ? Math.max(1, Math.ceil(ledger.data.totalCount / PAGE_SIZE)) : 1

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

      {/* Toolbar: filters + actions */}
      <div className="mb-4 flex flex-wrap items-end gap-3">
        <select
          value={filters.accountId}
          onChange={(e) => updateFilters({ accountId: e.target.value })}
          className={`${controlClass} mt-0 w-44`}
          aria-label="Filter by account"
        >
          <option value="">All accounts</option>
          {accounts.data?.map((a) => (
            <option key={a.id} value={a.id}>{a.name}</option>
          ))}
        </select>
        <select
          value={filters.categoryId}
          onChange={(e) => updateFilters({ categoryId: e.target.value })}
          className={`${controlClass} mt-0 w-48`}
          aria-label="Filter by category"
        >
          <option value="">All categories</option>
          {categories.data?.map((c) => (
            <option key={c.id} value={c.id}>{c.name}</option>
          ))}
        </select>
        <select
          value={filters.direction}
          onChange={(e) => updateFilters({ direction: e.target.value })}
          className={`${controlClass} mt-0 w-28`}
          aria-label="Filter by direction"
        >
          <option value="">In & out</option>
          <option value="In">Money in</option>
          <option value="Out">Money out</option>
        </select>
        <input
          type="date"
          value={filters.from}
          onChange={(e) => updateFilters({ from: e.target.value })}
          className={`${controlClass} mt-0 w-38`}
          aria-label="From date"
        />
        <input
          type="date"
          value={filters.to}
          onChange={(e) => updateFilters({ to: e.target.value })}
          className={`${controlClass} mt-0 w-38`}
          aria-label="To date"
        />
        <input
          type="search"
          placeholder="Search description, payee, notes…"
          value={filters.search}
          onChange={(e) => updateFilters({ search: e.target.value })}
          className={`${controlClass} mt-0 w-56 flex-1`}
          aria-label="Search transactions"
        />
        <div className="ml-auto flex gap-2">
          <Button variant="secondary" onClick={() => setTransferring(true)}>⇄ Transfer</Button>
          <Button onClick={() => setEditing({ mode: 'create' })}>+ Record Transaction</Button>
        </div>
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
                <th className="px-4 py-3">Date</th>
                <th className="px-4 py-3">Description</th>
                <th className="px-4 py-3">Category</th>
                <th className="px-4 py-3">Account</th>
                <th className="px-4 py-3">Payee / Payer</th>
                <th className="px-4 py-3 text-right">Amount</th>
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {ledger.data.items.map((t) => (
                <tr key={t.id} className="hover:bg-slate-50">
                  <td className="whitespace-nowrap px-4 py-3 text-slate-600">{formatDate(t.date)}</td>
                  <td className="px-4 py-3 font-medium text-slate-900">
                    {t.description}
                    {t.invoiceId && <RowTag>invoice</RowTag>}
                    {t.transferGroupId && <RowTag>transfer</RowTag>}
                    {t.attachments.length > 0 && <RowTag>📎 {t.attachments.length}</RowTag>}
                  </td>
                  <td className="px-4 py-3 text-slate-600">{t.categoryName}</td>
                  <td className="px-4 py-3 text-slate-600">{t.accountName}</td>
                  <td className="px-4 py-3 text-slate-600">{orDash(t.counterparty)}</td>
                  <td
                    className={`whitespace-nowrap px-4 py-3 text-right font-medium ${
                      t.direction === 'In' ? 'text-emerald-600' : 'text-red-600'
                    }`}
                  >
                    {t.direction === 'In' ? '+' : '−'}{currency(t.amount)}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3 text-right">
                    <Button variant="ghost" size="sm" onClick={() => setDetail(t)}>
                      Details
                    </Button>
                    {/* Invoice rows are corrected from the invoice; transfer legs are recreated, not edited. */}
                    {!t.invoiceId && !t.transferGroupId && (
                      <Button variant="ghost" size="sm" onClick={() => setEditing({ mode: 'edit', row: t })}>
                        Edit
                      </Button>
                    )}
                    {!t.invoiceId && (
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
              <Button variant="secondary" size="sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                ← Previous
              </Button>
              <Button variant="secondary" size="sm" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
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
        title={deleting?.transferGroupId ? 'Delete Transfer' : 'Delete Transaction'}
        message={
          deleting?.transferGroupId
            ? 'This removes both legs of the transfer from the ledger. This cannot be undone.'
            : deleting
              ? `Delete “${deleting.description}”? This cannot be undone.`
              : ''
        }
        onConfirm={handleDelete}
        onCancel={() => setDeleting(null)}
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

function RowTag({ children }: { children: React.ReactNode }) {
  return (
    <span className="ml-2 inline-block rounded bg-slate-100 px-1.5 py-0.5 text-xs font-normal text-slate-500">
      {children}
    </span>
  )
}

// --- Record / edit form --------------------------------------------------------

function TransactionForm({
  initial,
  accounts,
  categories,
  onSubmit,
  onCancel,
}: {
  initial: CashTransaction | null
  accounts: CashAccount[]
  categories: TransactionCategory[]
  onSubmit: (body: CashTransactionRequest) => Promise<void>
  onCancel: () => void
}) {
  const [accountId, setAccountId] = useState(initial?.accountId ?? accounts[0]?.id ?? '')
  const [direction, setDirection] = useState(initial?.direction ?? 'Out')
  const [amount, setAmount] = useState(initial ? String(initial.amount) : '')
  const [date, setDate] = useState(initial?.date ?? today())
  const [description, setDescription] = useState(initial?.description ?? '')
  const [categoryId, setCategoryId] = useState(initial?.categoryId ?? '')
  const [counterparty, setCounterparty] = useState(initial?.counterparty ?? '')
  const [gstTreatment, setGstTreatment] = useState(initial?.gstTreatment ?? '')
  const [notes, setNotes] = useState(initial?.notes ?? '')
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
        counterparty: counterparty.trim() || null,
        gstTreatment: gstTreatment || null,
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
        <input type="text" required maxLength={500} value={description} onChange={(e) => setDescription(e.target.value)} className={controlClass} />
      </Field>
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
      </div>
      <Field label="Payee / payer">
        <input type="text" maxLength={200} value={counterparty} onChange={(e) => setCounterparty(e.target.value)} className={controlClass} />
      </Field>
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

// --- Detail modal with attachments ------------------------------------------------

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
    <Modal open={transaction !== null} title="Transaction Details" onClose={onClose}>
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
          </div>
          <DetailRow label="Description" value={transaction.description} />
          {transaction.notes && <DetailRow label="Notes" value={transaction.notes} />}
          {transaction.invoiceId && (
            <p className="rounded-md bg-slate-50 px-3 py-2 text-xs text-slate-500">
              Posted automatically from an invoice payment — correct it from the invoice, not here.
            </p>
          )}
          {transaction.transferGroupId && (
            <p className="rounded-md bg-slate-50 px-3 py-2 text-xs text-slate-500">
              One leg of an account-to-account transfer; deleting it removes both legs.
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
        </div>
      )}
    </Modal>
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
