import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import {
  createCashAccount,
  deleteCashAccount,
  getCashAccounts,
  updateCashAccount,
} from '@/api/cashAccounts'
import { getCashFlowSettings, updateCashFlowSettings } from '@/api/cashFlowSettings'
import { CrudSection } from '@/components/crud/CrudSection'
import type { CrudFormProps } from '@/components/crud/CrudSection'
import { Button } from '@/components/ui/Button'
import { useToast } from '@/components/ui/toast'
import { Field, controlClass } from '@/components/forms/controls'
import { useAsync } from '@/hooks/useAsync'
import { currency, date as formatDate, orDash } from '@/lib/format'
import type { CashAccount, CashAccountRequest } from '@/types'
import { CASH_ACCOUNT_TYPES, CASH_ACCOUNT_TYPE_LABELS } from '@/types'

export function CashAccountsPage() {
  // Bumped after account mutations so the routing selects offer the fresh account list.
  const [accountsVersion, setAccountsVersion] = useState(0)

  return (
    <div className="space-y-10">
      <CrudSection<CashAccount>
        resourceName="Cash Account"
        title="Cash Accounts"
        description="Bank accounts, tills and wallets the business keeps money in. Balances are derived from the ledger and can't drift."
        load={() => getCashAccounts(true)}
        getId={(a) => a.id}
        rowLabel={(a) => a.name}
        columns={[
          {
            header: 'Name',
            cell: (a) => (
              <>
                {a.name}
                {a.isArchived && (
                  <span className="ml-2 rounded bg-slate-100 px-1.5 py-0.5 text-xs text-slate-500">archived</span>
                )}
              </>
            ),
            className: 'font-medium text-slate-900',
          },
          { header: 'Type', cell: (a) => CASH_ACCOUNT_TYPE_LABELS[a.type] ?? a.type },
          { header: 'Account number', cell: (a) => orDash(a.accountNumber) },
          { header: 'Opened', cell: (a) => `${formatDate(a.openingDate)} at ${currency(a.openingBalance)}` },
          {
            header: 'Current balance',
            cell: (a) => (
              <span className={a.currentBalance < 0 ? 'font-medium text-red-600' : 'font-medium text-slate-900'}>
                {currency(a.currentBalance)}
              </span>
            ),
          },
        ]}
        renderForm={(props) => <AccountForm {...props} />}
        onCreate={(v) => createCashAccount(v as unknown as CashAccountRequest).then(() => undefined)}
        onUpdate={(id, v) => updateCashAccount(id, v as unknown as CashAccountRequest).then(() => undefined)}
        onDelete={deleteCashAccount}
        onChanged={() => setAccountsVersion((v) => v + 1)}
      />

      <InvoiceRoutingSection accountsVersion={accountsVersion} />
    </div>
  )
}

function AccountForm({ initial, onSubmit, onCancel }: CrudFormProps<CashAccount>) {
  const [name, setName] = useState(initial?.name ?? '')
  const [type, setType] = useState(initial?.type ?? 'Bank')
  const [accountNumber, setAccountNumber] = useState(initial?.accountNumber ?? '')
  const [openingBalance, setOpeningBalance] = useState(initial ? String(initial.openingBalance) : '0')
  const [openingDate, setOpeningDate] = useState(initial?.openingDate ?? new Date().toISOString().slice(0, 10))
  const [isArchived, setIsArchived] = useState(initial?.isArchived ?? false)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)
    setBusy(true)
    try {
      await onSubmit({
        name: name.trim(),
        type,
        accountNumber: accountNumber.trim() || null,
        openingBalance: Number(openingBalance),
        openingDate,
        isArchived,
      })
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <Field label="Name" required>
        <input type="text" required maxLength={200} value={name} onChange={(e) => setName(e.target.value)} className={controlClass} />
      </Field>
      <div className="grid grid-cols-2 gap-4">
        <Field label="Type" required>
          <select required value={type} onChange={(e) => setType(e.target.value)} className={controlClass}>
            {CASH_ACCOUNT_TYPES.map((t) => (
              <option key={t} value={t}>{CASH_ACCOUNT_TYPE_LABELS[t]}</option>
            ))}
          </select>
        </Field>
        <Field label="Account number">
          <input type="text" maxLength={50} value={accountNumber} onChange={(e) => setAccountNumber(e.target.value)} className={controlClass} />
        </Field>
        <Field label="Opening balance" required>
          <input type="number" step="0.01" required value={openingBalance} onChange={(e) => setOpeningBalance(e.target.value)} className={controlClass} />
        </Field>
        <Field label="Opening date" required>
          <input type="date" required value={openingDate} onChange={(e) => setOpeningDate(e.target.value)} className={controlClass} />
        </Field>
      </div>
      {initial && (
        <label className="flex items-center gap-2 text-sm font-medium text-slate-700">
          <input type="checkbox" checked={isArchived} onChange={(e) => setIsArchived(e.target.checked)} />
          Archived (hidden from pickers; history is kept)
        </label>
      )}
      {error && <p className="text-sm text-red-600">{error}</p>}
      <div className="flex justify-end gap-2 pt-1">
        <Button type="button" variant="secondary" onClick={onCancel} disabled={busy}>Cancel</Button>
        <Button type="submit" disabled={busy}>
          {busy ? 'Saving…' : initial ? 'Save changes' : 'Create Cash Account'}
        </Button>
      </div>
    </form>
  )
}

/**
 * Where invoice payments land in the ledger: the cash portion, card portion and bank
 * transfers each route to an account, falling back to the default. While nothing is
 * configured, marking invoices paid simply doesn't post to the ledger.
 */
function InvoiceRoutingSection({ accountsVersion }: { accountsVersion: number }) {
  const toast = useToast()
  const accounts = useAsync(() => getCashAccounts(), [accountsVersion])
  const settings = useAsync(getCashFlowSettings, [])

  const [defaultAccountId, setDefaultAccountId] = useState('')
  const [cashAccountId, setCashAccountId] = useState('')
  const [cardAccountId, setCardAccountId] = useState('')
  const [bankTransferAccountId, setBankTransferAccountId] = useState('')
  const [safetyBufferAmount, setSafetyBufferAmount] = useState('0')
  const [lockDate, setLockDate] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (!settings.data) return
    setDefaultAccountId(settings.data.defaultAccountId ?? '')
    setCashAccountId(settings.data.cashAccountId ?? '')
    setCardAccountId(settings.data.cardAccountId ?? '')
    setBankTransferAccountId(settings.data.bankTransferAccountId ?? '')
    setSafetyBufferAmount(String(settings.data.safetyBufferAmount))
    setLockDate(settings.data.lockDate ?? '')
  }, [settings.data])

  const save = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    try {
      await updateCashFlowSettings({
        defaultAccountId: defaultAccountId || null,
        cashAccountId: cashAccountId || null,
        cardAccountId: cardAccountId || null,
        bankTransferAccountId: bankTransferAccountId || null,
        safetyBufferAmount: Number(safetyBufferAmount),
        lockDate: lockDate || null,
      })
      toast.success('Cash flow settings saved')
    } catch (err) {
      toast.error(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  const routeSelect = (label: string, value: string, set: (v: string) => void, fallbackLabel: string) => (
    <Field label={label}>
      <select value={value} onChange={(e) => set(e.target.value)} className={controlClass}>
        <option value="">{fallbackLabel}</option>
        {accounts.data?.map((a) => (
          <option key={a.id} value={a.id}>{a.name}</option>
        ))}
      </select>
    </Field>
  )

  return (
    <section>
      <h2 className="text-lg font-semibold text-slate-900">Invoice Payment Routing</h2>
      <p className="mt-1 text-sm text-slate-500">
        When an invoice is marked paid, its money is posted into these accounts automatically.
        Until a default is chosen, invoice payments are not posted to the ledger.
      </p>
      <form onSubmit={save} className="mt-4 max-w-2xl rounded-lg border border-slate-200 bg-white p-5">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          {routeSelect('Default account', defaultAccountId, setDefaultAccountId, 'Not set — payments not posted')}
          {routeSelect('Cash payments', cashAccountId, setCashAccountId, 'Use default account')}
          {routeSelect('Card payments', cardAccountId, setCardAccountId, 'Use default account')}
          {routeSelect('Bank transfers', bankTransferAccountId, setBankTransferAccountId, 'Use default account')}
          <Field label="Safety buffer">
            <input
              type="number"
              step="0.01"
              min="0"
              value={safetyBufferAmount}
              onChange={(e) => setSafetyBufferAmount(e.target.value)}
              className={controlClass}
            />
            <span className="mt-1 block text-xs font-normal text-slate-500">
              Minimum cash to keep on hand; the forecast flags the first date the balance is projected to drop below this.
            </span>
          </Field>
          <Field label="Lock transactions up to">
            <input
              type="date"
              value={lockDate}
              onChange={(e) => setLockDate(e.target.value)}
              className={controlClass}
            />
            <span className="mt-1 block text-xs font-normal text-slate-500">
              Transactions dated on or before this can't be added, edited or deleted — set it after handing figures to your accountant. Leave empty for no lock.
            </span>
          </Field>
        </div>
        <div className="mt-4 flex justify-end">
          <Button type="submit" disabled={busy || settings.loading}>
            {busy ? 'Saving…' : 'Save routing'}
          </Button>
        </div>
      </form>
    </section>
  )
}
