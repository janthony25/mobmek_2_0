import { useState } from 'react'
import type { ReactNode } from 'react'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { StateMessage } from '@/components/ui/StateMessage'
import { useToast } from '@/components/ui/toast'
import { useAsync } from '@/hooks/useAsync'
import { ResourceForm } from './ResourceForm'
import type { Column, FieldSchema } from './types'

/** Props passed to a custom form renderer (used for cascading forms). */
export interface CrudFormProps<T> {
  initial: T | null
  onSubmit: (values: Record<string, unknown>) => Promise<void>
  onCancel: () => void
}

interface CrudSectionProps<T> {
  /** Singular resource name, e.g. "Customer" — used in headings, buttons and toasts. */
  resourceName: string
  title?: string
  description?: string
  /** 'page' renders a large header; 'section' renders a compact one for embedding. */
  variant?: 'page' | 'section'

  load: () => Promise<T[]>
  /** Bump to force a reload from a parent (e.g. when scope changes). */
  reloadKey?: unknown
  getId: (row: T) => string
  rowLabel: (row: T) => string
  columns: Column<T>[]

  /** Schema for the built-in ResourceForm. Omit when supplying `renderForm`. */
  fields?: FieldSchema[]
  /** Custom form renderer for cascading/bespoke forms; replaces the schema form. */
  renderForm?: (props: CrudFormProps<T>) => ReactNode
  /** When set, a List/Cards toggle appears and this renders each row as a card. */
  renderCard?: (row: T) => ReactNode
  /** Initial view when `renderCard` is provided. Defaults to 'list'. */
  defaultView?: 'list' | 'cards'
  onCreate: (values: Record<string, unknown>) => Promise<void>
  onUpdate: (id: string, values: Record<string, unknown>) => Promise<void>
  onDelete: (id: string) => Promise<void>
  /** When set, the "Add" button calls this instead of opening the create modal (e.g. to navigate to a dedicated page). */
  onAdd?: () => void
  /** Fired after any successful create/update/delete (e.g. to refresh a parent). */
  onChanged?: () => void

  emptyText?: string
}

type Editing<T> = { mode: 'create' } | { mode: 'edit'; row: T } | null

export function CrudSection<T>({
  resourceName,
  title,
  description,
  variant = 'page',
  load,
  reloadKey,
  getId,
  rowLabel,
  columns,
  fields,
  renderForm,
  renderCard,
  defaultView,
  onCreate,
  onUpdate,
  onDelete,
  onAdd,
  onChanged,
  emptyText,
}: CrudSectionProps<T>) {
  const toast = useToast()
  const { data, loading, error, reload } = useAsync(load, [reloadKey])
  const [editing, setEditing] = useState<Editing<T>>(null)
  const [deleting, setDeleting] = useState<T | null>(null)
  const [view, setView] = useState<'list' | 'cards'>(defaultView ?? 'list')

  const heading = title ?? `${resourceName}s`
  const showCards = renderCard != null && view === 'cards'

  const handleSubmit = async (values: Record<string, unknown>) => {
    if (editing?.mode === 'edit') {
      await onUpdate(getId(editing.row), values)
      toast.success(`${resourceName} updated`)
    } else {
      await onCreate(values)
      toast.success(`${resourceName} created`)
    }
    setEditing(null)
    reload()
    onChanged?.()
  }

  const handleDelete = async () => {
    if (!deleting) return
    await onDelete(getId(deleting))
    toast.success(`${resourceName} deleted`)
    setDeleting(null)
    reload()
    onChanged?.()
  }

  return (
    <section>
      <div className="mb-4 flex items-end justify-between gap-4">
        <div>
          <h2
            className={
              variant === 'page'
                ? 'text-2xl font-semibold text-slate-900'
                : 'text-lg font-semibold text-slate-900'
            }
          >
            {heading}
          </h2>
          {description && <p className="mt-1 text-sm text-slate-500">{description}</p>}
        </div>
        <div className="flex items-center gap-2">
          {renderCard != null && (
            <div className="inline-flex rounded-md border border-slate-300 bg-white p-0.5">
              {(['cards', 'list'] as const).map((v) => (
                <button
                  key={v}
                  type="button"
                  onClick={() => setView(v)}
                  className={`rounded px-2.5 py-1 text-xs font-medium capitalize transition-colors ${
                    view === v ? 'bg-slate-900 text-white' : 'text-slate-600 hover:bg-slate-100'
                  }`}
                >
                  {v}
                </button>
              ))}
            </div>
          )}
          <Button onClick={onAdd ?? (() => setEditing({ mode: 'create' }))}>+ Add {resourceName}</Button>
        </div>
      </div>

      {loading && <StateMessage title={`Loading ${heading.toLowerCase()}…`} />}
      {error && <StateMessage title={`Could not load ${heading.toLowerCase()}`} description={error.message} />}
      {data && data.length === 0 && (
        <StateMessage title={emptyText ?? `No ${heading.toLowerCase()} yet`} description={`Use “Add ${resourceName}” to create one.`} />
      )}

      {data && data.length > 0 && showCards && (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {data.map((row) => (
            <div key={getId(row)}>{renderCard(row)}</div>
          ))}
        </div>
      )}

      {data && data.length > 0 && !showCards && (
        <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white">
          <table className="min-w-full divide-y divide-slate-200 text-sm">
            <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
              <tr>
                {columns.map((col) => (
                  <th key={col.header} className="px-4 py-3">
                    {col.header}
                  </th>
                ))}
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {data.map((row) => (
                <tr key={getId(row)} className="hover:bg-slate-50">
                  {columns.map((col) => (
                    <td key={col.header} className={`px-4 py-3 align-top ${col.className ?? 'text-slate-600'}`}>
                      {col.cell(row)}
                    </td>
                  ))}
                  <td className="whitespace-nowrap px-4 py-3 text-right">
                    <Button variant="ghost" size="sm" onClick={() => setEditing({ mode: 'edit', row })}>
                      Edit
                    </Button>
                    <Button variant="ghost" size="sm" className="text-red-600" onClick={() => setDeleting(row)}>
                      Delete
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <Modal
        open={editing !== null}
        title={editing?.mode === 'edit' ? `Edit ${resourceName}` : `Add ${resourceName}`}
        onClose={() => setEditing(null)}
      >
        {editing &&
          (renderForm ? (
            renderForm({
              initial: editing.mode === 'edit' ? editing.row : null,
              onSubmit: handleSubmit,
              onCancel: () => setEditing(null),
            })
          ) : (
            <ResourceForm
              fields={fields ?? []}
              initial={editing.mode === 'edit' ? (editing.row as Record<string, unknown>) : null}
              submitLabel={editing.mode === 'edit' ? 'Save changes' : `Create ${resourceName}`}
              onSubmit={handleSubmit}
              onCancel={() => setEditing(null)}
            />
          ))}
      </Modal>

      <ConfirmDialog
        open={deleting !== null}
        title={`Delete ${resourceName}`}
        message={deleting ? `Delete “${rowLabel(deleting)}”? This cannot be undone.` : ''}
        onConfirm={handleDelete}
        onCancel={() => setDeleting(null)}
      />
    </section>
  )
}
