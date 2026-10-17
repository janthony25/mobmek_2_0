import { useEffect, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { completeXeroConnection } from '@/api/xero'
import { ApiError } from '@/api/client'
import { Button } from '@/components/ui/Button'
import { StateMessage } from '@/components/ui/StateMessage'

/**
 * Landing page for Xero's OAuth redirect (the `Xero:RedirectUri` registered on the Xero app).
 * Deliberately a frontend route, not a backend endpoint: the auth cookie is SameSite=Strict, so
 * it would be dropped on the cross-site redirect arriving straight from login.xero.com. Once the
 * browser has landed back on our own origin, this page's own fetch to `complete` is same-site and
 * carries the cookie fine (docs/xero-integration-design.md §6).
 */
export function XeroCallbackPage() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState(false)
  const started = useRef(false)

  useEffect(() => {
    if (started.current) return
    started.current = true

    const code = searchParams.get('code')
    const state = searchParams.get('state')
    if (!code || !state) {
      setError('This link is missing its code/state — try connecting again from the Xero settings page.')
      return
    }

    completeXeroConnection(code, state)
      .then(() => setDone(true))
      .catch((err) => setError(err instanceof ApiError ? err.message : 'Failed to complete the Xero connection.'))
  }, [searchParams])

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-4">
      <div className="w-full max-w-sm rounded-lg border border-slate-200 bg-white p-8 text-center shadow-sm">
        {error ? (
          <>
            <StateMessage title="Could not connect to Xero" description={error} />
            <Button className="mt-4 w-full justify-center" onClick={() => navigate('/xero', { replace: true })}>
              Back to Xero settings
            </Button>
          </>
        ) : done ? (
          <>
            <p className="text-sm text-slate-700">Connected to Xero.</p>
            <Button className="mt-4 w-full justify-center" onClick={() => navigate('/xero', { replace: true })}>
              View connection status
            </Button>
          </>
        ) : (
          <StateMessage title="Completing the Xero connection…" loading />
        )}
      </div>
    </div>
  )
}
