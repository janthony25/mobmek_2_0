import { Fragment, useState } from 'react'
import type { ReactNode } from 'react'
import { Pagination } from './Pagination'
import { pageCount } from '@/lib/paging'

interface PaginatedListProps<T> {
  items: T[]
  pageSize: number
  getKey: (item: T) => string
  renderItem: (item: T) => ReactNode
}

/**
 * Client-side paginated version of the detail-page row lists: renders one page of
 * items in the shared divide-y style with a Pagination footer. Owns its page state.
 */
export function PaginatedList<T>({ items, pageSize, getKey, renderItem }: PaginatedListProps<T>) {
  const [page, setPage] = useState(1)
  // Clamp rather than reset so deleting the last row of the last page stays in range.
  const safePage = Math.min(page, pageCount(items.length, pageSize))
  const pageItems = items.slice((safePage - 1) * pageSize, safePage * pageSize)

  return (
    <>
      {/* Fragments (not wrapper divs) so the rows stay direct children — divide-y and
          the rows' own first:/last: padding selectors depend on that. */}
      <div className="divide-y divide-slate-300">
        {pageItems.map((item) => (
          <Fragment key={getKey(item)}>{renderItem(item)}</Fragment>
        ))}
      </div>
      <Pagination page={safePage} pageSize={pageSize} total={items.length} onPageChange={setPage} />
    </>
  )
}
