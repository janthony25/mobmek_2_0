import { useEffect, useMemo, useRef, useState } from 'react'
import { controlClass } from './controls'
import type { SelectOption } from '@/components/crud/types'

interface ComboboxProps {
  options: SelectOption[]
  /** Selected option value (an id), or '' when nothing is chosen. */
  value: string
  /** Fires with an option's value when one is picked, or '' when the selection is cleared. */
  onChange: (value: string) => void
  disabled?: boolean
  placeholder?: string
  /** Shown in the dropdown when the typed text matches no option. */
  emptyText?: string
}

/**
 * Type-ahead select: the user filters by typing, but a value is only emitted when
 * an option is actually chosen from the dropdown. Free-typed text that doesn't
 * match a selection leaves `value` empty, so callers can require a real pick.
 */
export function Combobox({
  options,
  value,
  onChange,
  disabled,
  placeholder,
  emptyText = 'No matches',
}: ComboboxProps) {
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState(false)
  const [highlight, setHighlight] = useState(0)
  const rootRef = useRef<HTMLDivElement>(null)

  const selectedLabel = useMemo(
    () => options.find((o) => o.value === value)?.label ?? '',
    [options, value],
  )

  // Keep the input text in sync with the selected option — covers editing an existing
  // record and options that load after the value was set.
  useEffect(() => {
    if (value) setQuery(selectedLabel)
  }, [value, selectedLabel])

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase()
    if (!q) return options
    return options.filter((o) => o.label.toLowerCase().includes(q))
  }, [options, query])

  // Close on outside click, discarding any unmatched typing.
  useEffect(() => {
    if (!open) return
    const onDocMouseDown = (e: MouseEvent) => {
      if (rootRef.current && !rootRef.current.contains(e.target as Node)) {
        setOpen(false)
        setQuery(selectedLabel)
      }
    }
    document.addEventListener('mousedown', onDocMouseDown)
    return () => document.removeEventListener('mousedown', onDocMouseDown)
  }, [open, selectedLabel])

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
      setHighlight((h) => Math.min(h + 1, filtered.length - 1))
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setHighlight((h) => Math.max(h - 1, 0))
    } else if (e.key === 'Enter') {
      if (open && filtered[highlight]) {
        e.preventDefault()
        choose(filtered[highlight])
      }
    } else if (e.key === 'Escape') {
      setOpen(false)
      setQuery(selectedLabel)
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
          {filtered.length === 0 ? (
            <li className="px-3 py-2 text-slate-400">{emptyText}</li>
          ) : (
            filtered.map((opt, i) => (
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
