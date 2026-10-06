import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { createRole, deleteRole, getPermissionCatalog, getRoles, setRolePermissions } from '@/api/roles'
import { Button } from '@/components/ui/Button'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { Modal } from '@/components/ui/Modal'
import { StateMessage } from '@/components/ui/StateMessage'
import { useToast } from '@/components/ui/toast'
import { Field, controlClass } from '@/components/forms/controls'
import { useAsync } from '@/hooks/useAsync'
import { PERMISSION_INFO } from '@/types'
import type { Role } from '@/types'

export function RolesPage() {
  const toast = useToast()
  const roles = useAsync(getRoles, [])
  const permissions = useAsync(getPermissionCatalog, [])
  const [addOpen, setAddOpen] = useState(false)
  const [name, setName] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)
  const [busyKey, setBusyKey] = useState<string | null>(null)
  const [deleting, setDeleting] = useState<Role | null>(null)

  if ((roles.loading && !roles.data) || (permissions.loading && !permissions.data)) {
    return <StateMessage title="Loading roles…" loading />
  }
  if (roles.error || permissions.error) {
    return <StateMessage title="Could not load roles" description={(roles.error ?? permissions.error)?.message} />
  }

  const catalog = permissions.data ?? []

  const openAdd = () => {
    setName('')
    setFormError(null)
    setAddOpen(true)
  }

  const handleCreate = async (event: FormEvent) => {
    event.preventDefault()
    if (!name.trim()) {
      setFormError('Enter a name.')
      return
    }
    setSubmitting(true)
    setFormError(null)
    try {
      await createRole({ name: name.trim() })
      toast.success(`Role "${name.trim()}" created`)
      setAddOpen(false)
      roles.reload()
    } catch (err) {
      setFormError(err instanceof Error ? err.message : String(err))
    } finally {
      setSubmitting(false)
    }
  }

  const togglePermission = async (role: Role, permission: string, checked: boolean) => {
    const next = checked
      ? [...role.permissions, permission]
      : role.permissions.filter((p) => p !== permission)

    setBusyKey(`${role.id}:${permission}`)
    try {
      await setRolePermissions(role.id, { permissions: next })
      roles.reload()
    } catch (err) {
      toast.error(err instanceof Error ? err.message : String(err))
    } finally {
      setBusyKey(null)
    }
  }

  const handleDelete = async () => {
    if (!deleting) return
    try {
      await deleteRole(deleting.id)
      toast.success(`Role "${deleting.name}" deleted`)
      setDeleting(null)
      roles.reload()
    } catch (err) {
      toast.error(err instanceof Error ? err.message : String(err))
      setDeleting(null)
    }
  }

  return (
    <section>
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900">Roles &amp; Permissions</h1>
          <p className="mt-1 text-sm text-slate-500">
            Define what each role can do, then assign it to an account from{' '}
            <Link to="/accounts" className="underline hover:text-slate-700">Accounts &amp; Roles</Link>.
            The Admin role always has every permission and can't be changed.
          </p>
        </div>
        <Button onClick={openAdd}>Add role</Button>
      </div>

      <div className="mt-6 space-y-4">
        {(roles.data ?? []).map((role) => (
          <div key={role.id} className="rounded-lg border border-slate-200 bg-white p-5">
            <div className="flex items-center justify-between">
              <div>
                <h2 className="text-base font-semibold text-slate-900">{role.name}</h2>
                <p className="text-xs text-slate-500">
                  {role.accountCount} {role.accountCount === 1 ? 'account' : 'accounts'}
                  {role.isProtected && ' · protected — always has every permission'}
                </p>
              </div>
              {role.isProtected ? (
                <span className="text-xs text-slate-400" title="The Admin role can't be deleted.">—</span>
              ) : role.accountCount > 0 ? (
                <span
                  className="text-xs text-slate-400"
                  title="This role is still assigned to one or more accounts — reassign them first."
                >
                  —
                </span>
              ) : (
                <Button variant="danger" size="sm" onClick={() => setDeleting(role)}>
                  Delete
                </Button>
              )}
            </div>

            <div className="mt-4 grid gap-3 sm:grid-cols-2">
              {catalog.map((permission) => {
                const info = PERMISSION_INFO[permission]
                const checked = role.isProtected || role.permissions.includes(permission)
                const busy = busyKey === `${role.id}:${permission}`
                return (
                  <label
                    key={permission}
                    className={`flex items-start gap-2 rounded-md border border-slate-200 p-2.5 text-sm ${
                      role.isProtected || busy ? 'opacity-60' : 'cursor-pointer hover:bg-slate-50'
                    }`}
                  >
                    <input
                      type="checkbox"
                      className="mt-0.5"
                      checked={checked}
                      disabled={role.isProtected || busy}
                      onChange={(e) => togglePermission(role, permission, e.target.checked)}
                    />
                    <span>
                      <span className="block font-medium text-slate-900">{info?.label ?? permission}</span>
                      {info?.description && <span className="block text-xs text-slate-500">{info.description}</span>}
                    </span>
                  </label>
                )
              })}
            </div>
          </div>
        ))}
        {(roles.data ?? []).length === 0 && (
          <p className="rounded-lg border border-slate-200 bg-white px-4 py-6 text-center text-slate-400">
            No roles yet.
          </p>
        )}
      </div>

      <Modal open={addOpen} title="Add role" onClose={() => setAddOpen(false)}>
        <form onSubmit={handleCreate} className="space-y-4">
          <Field label="Name" required>
            <input
              type="text"
              required
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="e.g. Front Desk"
              className={controlClass}
            />
          </Field>

          {formError && <p className="text-sm text-red-600">{formError}</p>}

          <div className="flex justify-end gap-2">
            <Button type="button" variant="secondary" onClick={() => setAddOpen(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={submitting}>
              {submitting ? 'Creating…' : 'Create role'}
            </Button>
          </div>
        </form>
      </Modal>

      <ConfirmDialog
        open={deleting !== null}
        title="Delete role"
        message={deleting ? `Delete the "${deleting.name}" role? This can't be undone.` : ''}
        confirmLabel="Delete"
        onConfirm={handleDelete}
        onCancel={() => setDeleting(null)}
      />
    </section>
  )
}
