import { useEffect } from 'react'

/**
 * Stops mouse-wheel scrolling from silently changing the value of a focused
 * `<input type="number">`. Without this, scrolling the page while a price/qty
 * field is focused nudges it by its `step` (e.g. 10 → 9.99), which reads as the
 * number "randomly" changing. Installed once at the app root so it covers every
 * number input, including config-driven forms.
 */
export function useNumberInputWheelGuard() {
  useEffect(() => {
    const onWheel = (event: WheelEvent) => {
      const el = document.activeElement
      if (el instanceof HTMLInputElement && el.type === 'number' && el === event.target) {
        // Blur so the browser doesn't apply the wheel to the value; the page
        // still scrolls normally.
        el.blur()
      }
    }
    document.addEventListener('wheel', onWheel, { passive: true })
    return () => document.removeEventListener('wheel', onWheel)
  }, [])
}
