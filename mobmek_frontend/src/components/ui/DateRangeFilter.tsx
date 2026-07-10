interface DateRangeFilterProps {
  dateFrom: string
  dateTo: string
  onDateFromChange: (value: string) => void
  onDateToChange: (value: string) => void
}

const dateInputClass =
  'w-36 rounded-md border border-slate-300 bg-white px-2 py-1 text-sm text-slate-700 focus:border-slate-500 focus:outline-none'

/** Two date inputs for a "from"/"to" range filter, with a clear link once either is set. */
export function DateRangeFilter({ dateFrom, dateTo, onDateFromChange, onDateToChange }: DateRangeFilterProps) {
  const hasRange = dateFrom !== '' || dateTo !== ''

  return (
    <div className="flex items-center gap-1.5">
      <input
        type="date"
        aria-label="From date"
        value={dateFrom}
        max={dateTo || undefined}
        onChange={(e) => onDateFromChange(e.target.value)}
        className={dateInputClass}
      />
      <span className="text-xs text-slate-400">to</span>
      <input
        type="date"
        aria-label="To date"
        value={dateTo}
        min={dateFrom || undefined}
        onChange={(e) => onDateToChange(e.target.value)}
        className={dateInputClass}
      />
      {hasRange && (
        <button
          type="button"
          onClick={() => {
            onDateFromChange('')
            onDateToChange('')
          }}
          className="text-xs text-slate-500 hover:underline"
        >
          Clear
        </button>
      )}
    </div>
  )
}
