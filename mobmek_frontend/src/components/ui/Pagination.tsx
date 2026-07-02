import { Button } from './Button'
import { pageCount } from '@/lib/paging'

interface PaginationProps {
  /** Current 1-based page. */
  page: number
  pageSize: number
  /** Total number of items across all pages. */
  total: number
  onPageChange: (page: number) => void
}

/**
 * Page numbers to render, with `null` marking an ellipsis gap. Every page is
 * shown up to 7; beyond that, the first, last and a window around the current page.
 */
function pageNumbers(page: number, totalPages: number): (number | null)[] {
  if (totalPages <= 7) return Array.from({ length: totalPages }, (_, i) => i + 1)
  const middle = [page - 1, page, page + 1].filter((p) => p > 1 && p < totalPages)
  return [
    1,
    ...(middle[0] !== undefined && middle[0] > 2 ? [null] : []),
    ...middle,
    ...(middle.length > 0 && middle[middle.length - 1]! < totalPages - 1 ? [null] : []),
    totalPages,
  ]
}

/** List footer with a range summary and Prev / page / Next controls. Renders nothing only when the list is empty. */
export function Pagination({ page, pageSize, total, onPageChange }: PaginationProps) {
  const totalPages = pageCount(total, pageSize)
  if (total === 0) return null

  const start = (page - 1) * pageSize + 1
  const end = Math.min(page * pageSize, total)

  return (
    <div className="mt-3 flex flex-wrap items-center justify-between gap-2">
      <p className="text-xs text-slate-500">
        Showing {start}–{end} of {total} · {pageSize} per page
      </p>
      <div className="flex items-center gap-1">
        <Button variant="ghost" size="sm" disabled={page === 1} onClick={() => onPageChange(page - 1)}>
          Prev
        </Button>
        {pageNumbers(page, totalPages).map((p, i) =>
          p === null ? (
            <span key={`gap-${i}`} className="px-1 text-xs text-slate-400">
              …
            </span>
          ) : (
            <button
              key={p}
              type="button"
              onClick={() => onPageChange(p)}
              aria-current={p === page ? 'page' : undefined}
              className={`min-w-7 rounded-md px-2 py-1 text-xs font-medium transition-colors ${
                p === page ? 'bg-slate-900 text-white' : 'text-slate-600 hover:bg-slate-100'
              }`}
            >
              {p}
            </button>
          ),
        )}
        <Button variant="ghost" size="sm" disabled={page === totalPages} onClick={() => onPageChange(page + 1)}>
          Next
        </Button>
      </div>
    </div>
  )
}
