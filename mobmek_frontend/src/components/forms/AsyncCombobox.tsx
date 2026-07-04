import { useEffect, useRef, useState } from 'react'
import { controlClass } from './controls'
import type { SelectOption } from '@/components/crud/types'

interface AsyncComboboxProps {
  /** Selected option value (an id), or '' when nothing is chosen. */
  value: string
  /** Fires with an option's value when one is picked, or '' when the selection is cleared. */
  onChange: (value: string) => void
  /** Fetches matches for the current query text; called with '' to show a default page. */
  search: (query: string) => Promise<SelectOption[]>
  /** Label for `value` before the user has opened the dropdown (e.g. editing an existing record). */
  initialLabel?: string | null
  disabled?: boolean
  placeholder?: string
  emptyText?: string
}

/**
 * Type-ahead select backed by a server search instead of a fully-fetched option list —
 * for pickers over tables too large to load in one go. Debounces the query and requests
 * a bounded page from `search` on every keystroke (and once on open, with an empty query,
 * to show a starting page). A value is only emitted when an option is actually chosen.
 */
export function AsyncCombobox({
  value,
  onChange,
  search,
  initialLabel,
  disabled,
  placeholder,
  emptyText = 'No matches',
}: AsyncComboboxProps) {
  const [query, setQuery] = useState(initialLabel ?? '')
  const [open, setOpen] = useState(false)
  const [options, setOptions] = useState<SelectOption[]>([])
  const [loading, setLoading] = useState(false)
  const [highlight, setHighlight] = useState(0)
  const rootRef = useRef<HTMLDivElement>(null)
  const requestId = useRef(0)

  // Keep the input text in sync with the selected value's label when it changes
  // from outside (e.g. loading an existing record, or the picker being cleared).
  useEffect(() => {
    setQuery(value ? (initialLabel ?? '') : '')
  }, [value, initialLabel])

  // Debounce the query and ask the server for matches. Fires once on open (with
  // whatever text is already there) so the dropdown isn't empty on first focus.
  useEffect(() => {
    if (!open) return
    const id = ++requestId.current
    setLoading(true)
    const handle = setTimeout(() => {
      search(query.trim())
        .then((results) => {
          if (requestId.current === id) setOptions(results)
        })
        .catch(() => {
          if (requestId.current === id) setOptions([])
        })
        .finally(() => {
          if (requestId.current === id) setLoading(false)
        })
    }, 300)
    return () => clearTimeout(handle)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [query, open])

  // Close on outside click, discarding any unmatched typing.
  useEffect(() => {
    if (!open) return
    const onDocMouseDown = (e: MouseEvent) => {
      if (rootRef.current && !rootRef.current.contains(e.target as Node)) {
        setOpen(false)
        setQuery(value ? (initialLabel ?? '') : '')
      }
    }
    document.addEventListener('mousedown', onDocMouseDown)
    return () => document.removeEventListener('mousedown', onDocMouseDown)
  }, [open, value, initialLabel])

  const choose = (opt: SelectOption) => {
    onChange(opt.value)
    setQuery(opt.label)
    setOpen(false)
  }

  const handleInput = (text: string) => {
    setQuery(text)
    setOpen(true)
    setHighlight(0)
    // Editing the text invalidates any prior pick; the caller must require a real selection.
    if (value) onChange('')
  }

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      setOpen(true)
      setHighlight((h) => Math.min(h + 1, options.length - 1))
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setHighlight((h) => Math.max(h - 1, 0))
    } else if (e.key === 'Enter') {
      if (open && options[highlight]) {
        e.preventDefault()
        choose(options[highlight])
      }
    } else if (e.key === 'Escape') {
      setOpen(false)
      setQuery(value ? (initialLabel ?? '') : '')
    }
  }

  return (
    <div ref={rootRef} className="relative">
      <input
        type="text"
        role="combobox"
        aria-expanded={open}
        aria-autocomplete="list"
        autoComplete="off"
        value={query}
        disabled={disabled}
        placeholder={placeholder}
        onChange={(e) => handleInput(e.target.value)}
        onFocus={() => setOpen(true)}
        onKeyDown={handleKeyDown}
        className={`${controlClass} ${disabled ? 'bg-slate-50 text-slate-400' : ''}`}
      />
      {open && !disabled && (
        <ul className="absolute z-10 mt-1 max-h-56 w-full overflow-auto rounded-md border border-slate-200 bg-white py-1 text-sm shadow-lg">
          {loading ? (
            <li className="px-3 py-2 text-slate-400">Searching…</li>
          ) : options.length === 0 ? (
            <li className="px-3 py-2 text-slate-400">{emptyText}</li>
          ) : (
            options.map((opt, i) => (
              <li key={opt.value}>
                <button
                  type="button"
                  // Use mouseDown so the pick lands before the input's blur/outside-click.
                  onMouseDown={(e) => {
                    e.preventDefault()
                    choose(opt)
                  }}
                  onMouseEnter={() => setHighlight(i)}
                  className={`block w-full px-3 py-2 text-left ${
                    i === highlight ? 'bg-slate-100 text-slate-900' : 'text-slate-700'
                  } ${opt.value === value ? 'font-medium' : ''}`}
                >
                  {opt.label}
                </button>
              </li>
            ))
          )}
        </ul>
      )}
    </div>
  )
}
