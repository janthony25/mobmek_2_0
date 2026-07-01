import { useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { Button } from './Button'

export interface DropdownMenuItem {
  label: string
  onClick?: () => void
  disabled?: boolean
  /** Small hint shown beside a disabled item, e.g. "Coming soon". */
  hint?: string
  tone?: 'default' | 'danger'
}

interface DropdownMenuProps {
  label: string
  items: DropdownMenuItem[]
}

const PANEL_WIDTH = 208 // w-52

/**
 * Trigger button + action list rendered through a portal (so it isn't clipped by an
 * ancestor's `overflow-x-auto`, which browsers also compute as clipping the y-axis).
 * Closes on outside click, Escape, or scroll/resize.
 */
export function DropdownMenu({ label, items }: DropdownMenuProps) {
  const [open, setOpen] = useState(false)
  const [coords, setCoords] = useState<{ top: number; left: number } | null>(null)
  const triggerRef = useRef<HTMLDivElement>(null)
  const panelRef = useRef<HTMLDivElement>(null)

  const openMenu = () => {
    const rect = triggerRef.current?.getBoundingClientRect()
    if (rect) {
      setCoords({ top: rect.bottom + 4, left: Math.max(8, rect.right - PANEL_WIDTH) })
    }
    setOpen(true)
  }

  useEffect(() => {
    if (!open) return

    const onDocClick = (e: MouseEvent) => {
      const target = e.target as Node
      if (triggerRef.current?.contains(target) || panelRef.current?.contains(target)) return
      setOpen(false)
    }
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false)
    }
    const onScrollOrResize = () => setOpen(false)

    document.addEventListener('mousedown', onDocClick)
    window.addEventListener('keydown', onKey)

    // Focusing the trigger button can itself scroll an ancestor (e.g. a horizontally
    // overflowing table) into view; attach the scroll/resize close-listener a frame
    // later so that self-inflicted scroll doesn't immediately close the menu.
    const raf = requestAnimationFrame(() => {
      window.addEventListener('scroll', onScrollOrResize, true)
      window.addEventListener('resize', onScrollOrResize)
    })

    return () => {
      cancelAnimationFrame(raf)
      document.removeEventListener('mousedown', onDocClick)
      window.removeEventListener('keydown', onKey)
      window.removeEventListener('scroll', onScrollOrResize, true)
      window.removeEventListener('resize', onScrollOrResize)
    }
  }, [open])

  return (
    <div ref={triggerRef} className="inline-block">
      <Button type="button" variant="secondary" size="sm" onClick={() => (open ? setOpen(false) : openMenu())}>
        {label} <span aria-hidden>▾</span>
      </Button>
      {open &&
        coords &&
        createPortal(
          <div
            ref={panelRef}
            role="menu"
            style={{ position: 'fixed', top: coords.top, left: coords.left, width: PANEL_WIDTH }}
            className="z-50 overflow-hidden rounded-md border border-slate-200 bg-white py-1 shadow-lg"
          >
            {items.map((item) => (
              <button
                key={item.label}
                type="button"
                role="menuitem"
                disabled={item.disabled}
                onClick={() => {
                  if (item.disabled) return
                  setOpen(false)
                  item.onClick?.()
                }}
                className={[
                  'flex w-full items-center justify-between gap-3 px-3 py-2 text-left text-sm',
                  item.disabled
                    ? 'cursor-not-allowed text-slate-300'
                    : item.tone === 'danger'
                      ? 'text-red-600 hover:bg-red-50'
                      : 'text-slate-700 hover:bg-slate-50',
                ].join(' ')}
              >
                <span>{item.label}</span>
                {item.hint && <span className="text-xs text-slate-400">{item.hint}</span>}
              </button>
            ))}
          </div>,
          document.body,
        )}
    </div>
  )
}
