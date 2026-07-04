import { useCallback, useEffect, useRef, useState } from 'react'

interface AsyncState<T> {
  data: T | null
  loading: boolean
  error: Error | null
  reload: () => void
}

/**
 * Runs an async function on mount (and whenever `deps` change) and exposes its
 * loading / error / data state. `reload` re-runs the same function on demand.
 *
 * The previous result is kept on screen while a fetch is in flight (only `loading`
 * flips) — a list/detail view never briefly reads as empty or not-found between two
 * fetches of the same query. Callers whose `deps` represent a genuinely different
 * entity (e.g. a different id) should key the component so React remounts it, rather
 * than relying on this hook to clear stale data.
 */
export function useAsync<T>(fn: () => Promise<T>, deps: unknown[] = []): AsyncState<T> {
  const [data, setData] = useState<T | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<Error | null>(null)

  // The caller owns `fn`'s identity via the explicit dependency list.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const run = useCallback(fn, deps)

  // Guards against an older, slower call clobbering a newer one's result.
  const callIdRef = useRef(0)

  const execute = useCallback(() => {
    const callId = ++callIdRef.current
    setLoading(true)
    setError(null)

    run()
      .then((result) => {
        if (callIdRef.current === callId) setData(result)
      })
      .catch((err: unknown) => {
        if (callIdRef.current === callId) setError(err instanceof Error ? err : new Error(String(err)))
      })
      .finally(() => {
        if (callIdRef.current === callId) setLoading(false)
      })
  }, [run])

  useEffect(() => {
    execute()
  }, [execute])

  return { data, loading, error, reload: execute }
}
