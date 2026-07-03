import { useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { Pagination } from '@/components/ui/Pagination'
import { StateMessage } from '@/components/ui/StateMessage'
import { useToast } from '@/components/ui/toast'
import { useAsync } from '@/hooks/useAsync'
import { ResourceForm } from './ResourceForm'
import type { Column, FieldSchema } from './types'
import { pageCount } from '@/lib/paging'
import type { PagedResult } from '@/types'

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
  /** When set, the heading toggles the section body (starts collapsed while empty, expanded once there's data). */
  collapsible?: boolean

  /** Loads the full list; paginate client-side via `pageSize`. Use `loadPaged` instead for server-side pagination. */
  load?: () => Promise<T[]>
  /** Server-side mode: fetches one page at a time and shows a search box. Replaces `load`. */
  loadPaged?: (query: { page: number; pageSize: number; search: string }) => Promise<PagedResult<T>>
  /** Rows per page. With `load`, slices client-side; with `loadPaged`, sent to the server. Omit for no pagination. */
  pageSize?: number
  /** Rows per page for the cards view (defaults to `pageSize`). */
  cardsPageSize?: number
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

  /** Optional extra per-row action rendered before Edit/Delete (e.g. "Mark as done"). */
  extraAction?: {
    label: (row: T) => string
    onClick: (row: T) => Promise<void>
    /** Hide the action for a given row, e.g. once it's already done. */
    hidden?: (row: T) => boolean
  }

  emptyText?: string
}

type Editing<T> = { mode: 'create' } | { mode: 'edit'; row: T } | null

export function CrudSection<T>({
  resourceName,
  title,
  description,
  variant = 'page',
  collapsible = false,
  load,
  loadPaged,
  pageSize,
  cardsPageSize,
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
  extraAction,
  emptyText,
}: CrudSectionProps<T>) {
  const toast = useToast()
  const [editing, setEditing] = useState<Editing<T>>(null)
  const [deleting, setDeleting] = useState<T | null>(null)
  const [view, setView] = useState<'list' | 'cards'>(defaultView ?? 'list')
  // Collapsed until data proves there's something to show, unless the user has toggled it.
  const [collapsedOverride, setCollapsedOverride] = useState<boolean | null>(null)
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')

  const heading = title ?? `${resourceName}s`
  const showCards = renderCard != null && view === 'cards'
  const serverMode = loadPaged != null
  const effectivePageSize = showCards ? (cardsPageSize ?? pageSize) : pageSize

  // Debounce the search box so we don't hit the API on every keystroke.
  useEffect(() => {
    const handle = setTimeout(() => {
      setDebouncedSearch(search.trim())
      setPage(1)
    }, 300)
    return () => clearTimeout(handle)
  }, [search])

  // In server mode a page/size/search change refetches; in client mode those deps are inert.
  const { data: result, loading, error, reload } = useAsync(
    async (): Promise<{ rows: T[]; total: number; fetchedPage: number }> => {
      if (loadPaged) {
        const res = await loadPaged({ page, pageSize: effectivePageSize ?? 20, search: debouncedSearch })
        return { rows: res.items, total: res.totalCount, fetchedPage: res.page }
      }
      const rows = await load!()
      return { rows, total: rows.length, fetchedPage: 1 }
    },
    [reloadKey, serverMode && page, serverMode && effectivePageSize, serverMode && debouncedSearch],
  )

  const total = result?.total ?? 0
  const collapsed = collapsible && (collapsedOverride ?? (result == null || total === 0))
  const totalPages = effectivePageSize ? pageCount(total, effectivePageSize) : 1
  // Clamp rather than reset so deleting the last row of the last page stays in range.
  const safePage = Math.min(page, totalPages)
  const rows =
    result == null
      ? null
      : serverMode || !effectivePageSize
        ? result.rows
        : result.rows.slice((safePage - 1) * effectivePageSize, safePage * effectivePageSize)
  // In server mode, describe the page the rows actually came from — `page` runs ahead of
  // `result` for a frame between a pager click and the refetch landing.
  const displayPage = serverMode ? result?.fetchedPage ?? 1 : safePage

  // Server mode returns an empty page once the last row of the last page is deleted;
  // step back so the pager stays in range (client mode clamps via safePage instead).
  useEffect(() => {
    if (serverMode && result && page > totalPages) setPage(totalPages)
  }, [serverMode, result, page, totalPages])

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

  const handleExtraAction = async (row: T) => {
    if (!extraAction) return
    await extraAction.onClick(row)
    toast.success(`${resourceName} updated`)
    reload()
    onChanged?.()
  }

  return (
    <section>
      <div className={`flex items-end justify-between gap-4 ${collapsed ? '' : 'mb-4'}`}>
        <div>
          {collapsible ? (
            <button
              type="button"
              onClick={() => setCollapsedOverride(!collapsed)}
              aria-expanded={!collapsed}
              className="flex items-center gap-2"
            >
              <h2
                className={
                  variant === 'page'
                    ? 'text-2xl font-semibold text-slate-900'
                    : 'text-lg font-semibold text-slate-900'
                }
              >
                {heading}
              </h2>
              <span aria-hidden className="text-sm text-slate-400">{collapsed ? '▸' : '▾'}</span>
            </button>
          ) : (
            <h2
              className={
                variant === 'page'
                  ? 'text-2xl font-semibold text-slate-900'
                  : 'text-lg font-semibold text-slate-900'
              }
            >
              {heading}
            </h2>
          )}
          {description && <p className="mt-1 text-sm text-slate-500">{description}</p>}
        </div>
        <div className="flex items-center gap-2">
          {serverMode && (
            <input
              type="search"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={`Search ${heading.toLowerCase()}…`}
              className="w-48 rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm text-slate-700 placeholder:text-slate-400 focus:border-slate-500 focus:outline-none"
            />
          )}
          {renderCard != null && (
            <div className="inline-flex rounded-md border border-slate-300 bg-white p-0.5">
              {(['cards', 'list'] as const).map((v) => (
                <button
                  key={v}
                  type="button"
                  onClick={() => {
                    setView(v)
                    setPage(1)
                  }}
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

      {!collapsed && loading && <StateMessage title={`Loading ${heading.toLowerCase()}…`} />}
      {!collapsed && error && <StateMessage title={`Could not load ${heading.toLowerCase()}`} description={error.message} />}
      {!collapsed && rows && total === 0 && (
        <StateMessage
          title={debouncedSearch ? `No ${heading.toLowerCase()} match “${debouncedSearch}”` : emptyText ?? `No ${heading.toLowerCase()} yet`}
          description={debouncedSearch ? 'Try a different search.' : `Use “Add ${resourceName}” to create one.`}
        />
      )}

      {!collapsed && rows && rows.length > 0 && showCards && (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {rows.map((row) => (
            <div key={getId(row)}>{renderCard(row)}</div>
          ))}
        </div>
      )}

      {!collapsed && rows && rows.length > 0 && !showCards && (
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
              {rows.map((row) => (
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
                    {extraAction && !extraAction.hidden?.(row) && (
                      <Button variant="ghost" size="sm" onClick={() => handleExtraAction(row)}>
                        {extraAction.label(row)}
                      </Button>
                    )}
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

      {!collapsed && !loading && total > 0 && effectivePageSize != null && (
        <Pagination page={displayPage} pageSize={effectivePageSize} total={total} onPageChange={setPage} />
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
