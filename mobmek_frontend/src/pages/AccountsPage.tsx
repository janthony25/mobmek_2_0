import { useState } from 'react'
import type { FormEvent } from 'react'
import { createAccount, deactivateAccount, getAccounts, reactivateAccount, updateAccountRole } from '@/api/accounts'
import { getEmployees } from '@/api/employees'
import { Button } from '@/components/ui/Button'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { Modal } from '@/components/ui/Modal'
import { StateMessage } from '@/components/ui/StateMessage'
import { useToast } from '@/components/ui/toast'
import { Field, controlClass } from '@/components/forms/controls'
import { useAuth } from '@/contexts/AuthContext'
import { useAsync } from '@/hooks/useAsync'
import { date } from '@/lib/format'
import type { AccountListItem, AccountRole } from '@/types'

const ROLES: AccountRole[] = ['Admin', 'Employee']
const DELETION_GRACE_PERIOD_DAYS = 30

const daysUntilDeletion = (deactivatedAtUtc: string): number => {
  const daysSinceDeactivation = Math.floor((Date.now() - new Date(deactivatedAtUtc).getTime()) / (1000 * 60 * 60 * 24))
  return Math.max(0, DELETION_GRACE_PERIOD_DAYS - daysSinceDeactivation)
}

export function AccountsPage() {
  const toast = useToast()
  const { user } = useAuth()
  const accounts = useAsync(getAccounts, [])
  const employees = useAsync(getEmployees, [])
  const [addOpen, setAddOpen] = useState(false)
  const [employeeId, setEmployeeId] = useState('')
  const [email, setEmail] = useState('')
  const [role, setRole] = useState<AccountRole>('Employee')
  const [submitting, setSubmitting] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)
  const [roleBusyUserId, setRoleBusyUserId] = useState<string | null>(null)
  const [actionBusyUserId, setActionBusyUserId] = useState<string | null>(null)
  const [deactivating, setDeactivating] = useState<AccountListItem | null>(null)

  if ((accounts.loading && !accounts.data) || (employees.loading && !employees.data)) {
    return <StateMessage title="Loading accounts…" loading />
  }
  if (accounts.error || employees.error) {
    return <StateMessage title="Could not load accounts" description={(accounts.error ?? employees.error)?.message} />
  }

  const linkedEmployeeIds = new Set((accounts.data ?? []).map((a) => a.employeeId))
  const availableEmployees = (employees.data ?? []).filter((e) => !linkedEmployeeIds.has(e.id))

  const openAdd = () => {
    const first = availableEmployees[0]
    setEmployeeId(first?.id ?? '')
    setEmail(first?.emailAddress ?? '')
    setRole('Employee')
    setFormError(null)
    setAddOpen(true)
  }

  const onEmployeeChange = (id: string) => {
    setEmployeeId(id)
    const employee = availableEmployees.find((e) => e.id === id)
    if (employee) setEmail(employee.emailAddress)
  }

  const handleCreate = async (event: FormEvent) => {
    event.preventDefault()
    if (!employeeId) {
      setFormError('Choose an employee.')
      return
    }
    setSubmitting(true)
    setFormError(null)
    try {
      await createAccount({ employeeId, email, role })
      toast.success(`Activation link sent to ${email}`)
      setAddOpen(false)
      accounts.reload()
    } catch (err) {
      setFormError(err instanceof Error ? err.message : String(err))
    } finally {
      setSubmitting(false)
    }
  }

  const handleRoleChange = async (userId: string, newRole: AccountRole) => {
    setRoleBusyUserId(userId)
    try {
      await updateAccountRole(userId, { role: newRole })
      toast.success('Role updated')
      accounts.reload()
    } catch (err) {
      toast.error(err instanceof Error ? err.message : String(err))
    } finally {
      setRoleBusyUserId(null)
    }
  }

  const handleDeactivate = async () => {
    if (!deactivating) return
    await deactivateAccount(deactivating.userId)
    toast.success(`${deactivating.firstName} ${deactivating.lastName}'s account was deactivated`)
    setDeactivating(null)
    accounts.reload()
  }

  const handleReactivate = async (userId: string) => {
    setActionBusyUserId(userId)
    try {
      await reactivateAccount(userId)
      toast.success('Account reactivated')
      accounts.reload()
    } catch (err) {
      toast.error(err instanceof Error ? err.message : String(err))
    } finally {
      setActionBusyUserId(null)
    }
  }

  return (
    <section>
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900">Accounts &amp; Roles</h1>
          <p className="mt-1 text-sm text-slate-500">
            Create login accounts for employees and manage who has Admin access. New accounts must
            use the emailed activation link to set a password before they can sign in.
          </p>
        </div>
        <Button onClick={openAdd} disabled={availableEmployees.length === 0}>
          Add account
        </Button>
      </div>

      <div className="mt-6 overflow-x-auto rounded-lg border border-slate-200 bg-white">
        <table className="min-w-full divide-y divide-slate-300 text-sm">
          <thead className="bg-slate-50 text-left text-xs font-medium uppercase tracking-wide text-slate-500">
            <tr>
              <th className="px-4 py-3">Name</th>
              <th className="px-4 py-3">Email</th>
              <th className="px-4 py-3">Role</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3 text-right">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-300">
            {(accounts.data ?? []).map((account) => (
              <tr key={account.userId}>
                <td className="px-4 py-3 font-medium text-slate-900">
                  {account.firstName} {account.lastName}
                </td>
                <td className="px-4 py-3 text-slate-600">{account.email}</td>
                <td className="px-4 py-3">
                  {account.userId === user?.id ? (
                    <span title="You can't change your own role — ask another Admin to do it.">
                      {account.roles[0] ?? 'Employee'}
                    </span>
                  ) : (
                    <select
                      value={account.roles[0] ?? 'Employee'}
                      disabled={roleBusyUserId === account.userId}
                      onChange={(e) => handleRoleChange(account.userId, e.target.value as AccountRole)}
                      className="rounded-md border border-slate-300 px-2 py-1 text-sm shadow-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500 disabled:opacity-50"
                    >
                      {ROLES.map((r) => (
                        <option key={r} value={r}>
                          {r}
                        </option>
                      ))}
                    </select>
                  )}
                </td>
                <td className="px-4 py-3">
                  {account.deactivatedAtUtc ? (
                    <span
                      className="inline-flex items-center rounded-full bg-red-50 px-2 py-0.5 text-xs font-medium text-red-700"
                      title={`Deactivated ${date(account.deactivatedAtUtc)}`}
                    >
                      Deactivated — deletes in {daysUntilDeletion(account.deactivatedAtUtc)}d
                    </span>
                  ) : account.isActive ? (
                    <span className="inline-flex items-center rounded-full bg-green-50 px-2 py-0.5 text-xs font-medium text-green-700">
                      Active
                    </span>
                  ) : (
                    <span className="inline-flex items-center rounded-full bg-amber-50 px-2 py-0.5 text-xs font-medium text-amber-700">
                      Pending confirmation
                    </span>
                  )}
                </td>
                <td className="px-4 py-3 text-right">
                  {account.userId === user?.id ? (
                    <span className="text-xs text-slate-400" title="You can't deactivate your own account — ask another Admin to do it.">
                      —
                    </span>
                  ) : account.deactivatedAtUtc ? (
                    <Button
                      variant="secondary"
                      size="sm"
                      disabled={actionBusyUserId === account.userId}
                      onClick={() => handleReactivate(account.userId)}
                    >
                      Reactivate
                    </Button>
                  ) : (
                    <Button variant="danger" size="sm" onClick={() => setDeactivating(account)}>
                      Deactivate
                    </Button>
                  )}
                </td>
              </tr>
            ))}
            {(accounts.data ?? []).length === 0 && (
              <tr>
                <td colSpan={5} className="px-4 py-6 text-center text-slate-400">
                  No accounts yet.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <Modal open={addOpen} title="Add account" onClose={() => setAddOpen(false)}>
        <form onSubmit={handleCreate} className="space-y-4">
          <Field label="Employee" required>
            <select
              value={employeeId}
              onChange={(e) => onEmployeeChange(e.target.value)}
              className={controlClass}
            >
              {availableEmployees.length === 0 && <option value="">No employees without an account</option>}
              {availableEmployees.map((e) => (
                <option key={e.id} value={e.id}>
                  {e.firstName} {e.lastName}
                </option>
              ))}
            </select>
          </Field>
          <Field label="Login email" required>
            <input
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className={controlClass}
            />
          </Field>
          <Field label="Role" required>
            <select value={role} onChange={(e) => setRole(e.target.value as AccountRole)} className={controlClass}>
              {ROLES.map((r) => (
                <option key={r} value={r}>
                  {r}
                </option>
              ))}
            </select>
          </Field>

          {formError && <p className="text-sm text-red-600">{formError}</p>}

          <div className="flex justify-end gap-2">
            <Button type="button" variant="secondary" onClick={() => setAddOpen(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={submitting || !employeeId}>
              {submitting ? 'Sending…' : 'Create & send activation link'}
            </Button>
          </div>
        </form>
      </Modal>

      <ConfirmDialog
        open={deactivating !== null}
        title="Deactivate account"
        message={
          deactivating
            ? `Deactivate ${deactivating.firstName} ${deactivating.lastName}'s account? They'll be signed out immediately and won't be able to sign in again. The account is permanently deleted after ${DELETION_GRACE_PERIOD_DAYS} days unless you reactivate it before then.`
            : ''
        }
        confirmLabel="Deactivate"
        onConfirm={handleDeactivate}
        onCancel={() => setDeactivating(null)}
      />
    </section>
  )
}
