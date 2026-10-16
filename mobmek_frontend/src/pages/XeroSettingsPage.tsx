import { useState } from 'react'
import { connectXero, disconnectXero, getXeroStatus } from '@/api/xero'
import { ApiError } from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { StateMessage } from '@/components/ui/StateMessage'
import { useToast } from '@/components/ui/toast'
import { useAsync } from '@/hooks/useAsync'
import { dateTime, orDash } from '@/lib/format'

/**
 * Connect/disconnect and status for the Xero integration (docs/xero-integration-design.md §6).
 * Phase 0 only: this page just establishes and monitors the connection — generated invoices
 * don't push to Xero yet (that's Phase 1, see the design doc).
 */
export function XeroSettingsPage() {
  const toast = useToast()
  const { data, loading, error, reload } = useAsync(getXeroStatus, [])
  const [connecting, setConnecting] = useState(false)
  const [disconnecting, setDisconnecting] = useState(false)
  const [confirmingDisconnect, setConfirmingDisconnect] = useState(false)

  if (loading && !data) return <StateMessage title="Loading Xero connection status…" loading />
  if (error) return <StateMessage title="Could not load Xero connection status" description={error.message} />

  const connect = async () => {
    setConnecting(true)
    try {
      const { authorizationUrl } = await connectXero()
      window.location.href = authorizationUrl
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Failed to start the Xero connection.')
      setConnecting(false)
    }
  }

  const disconnect = async () => {
    setDisconnecting(true)
    try {
      await disconnectXero()
      toast.success('Disconnected from Xero.')
      reload()
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Failed to disconnect.')
    } finally {
      setDisconnecting(false)
      setConfirmingDisconnect(false)
    }
  }

  const statusTone = data?.status === 'Connected' ? 'green' : data?.status === 'Error' ? 'red' : 'amber'

  return (
    <section className="max-w-xl">
      <h1 className="text-2xl font-semibold text-slate-900">Xero</h1>
      <p className="mt-1 text-sm text-slate-500">
        Connects this app to your Xero organisation for bookkeeping/reconciliation visibility. Invoices don't push
        to Xero yet — this page only establishes the connection.
      </p>

      <div className="mt-6 rounded-lg border border-slate-200 bg-white p-5">
        <div className="mb-4 flex items-center gap-2">
          <span className="text-sm font-medium text-slate-700">Status</span>
          {!data?.configured ? (
            <Badge tone="amber">Not configured — set Xero:ClientId / :ClientSecret / :RedirectUri</Badge>
          ) : (
            <Badge tone={statusTone}>{data.status}</Badge>
          )}
        </div>

        <dl className="grid grid-cols-1 gap-x-6 gap-y-3 text-sm sm:grid-cols-2">
          <div>
            <dt className="text-slate-500">Connected organisation</dt>
            <dd className="font-medium text-slate-900">{orDash(data?.tenantName)}</dd>
          </div>
          <div>
            <dt className="text-slate-500">Connected since</dt>
            <dd className="font-medium text-slate-900">{dateTime(data?.connectedAtUtc)}</dd>
          </div>
          <div className="sm:col-span-2">
            <dt className="text-slate-500">Last error</dt>
            <dd className={data?.lastError ? 'font-medium text-red-700' : 'font-medium text-slate-900'}>
              {orDash(data?.lastError)}
            </dd>
          </div>
        </dl>

        <div className="mt-4 flex gap-3">
          {data?.connected ? (
            <Button variant="danger" onClick={() => setConfirmingDisconnect(true)} disabled={disconnecting}>
              {disconnecting ? 'Disconnecting…' : 'Disconnect'}
            </Button>
          ) : (
            <Button onClick={connect} disabled={connecting || !data?.configured}>
              {connecting ? 'Redirecting to Xero…' : 'Connect to Xero'}
            </Button>
          )}
        </div>
        {!data?.configured && (
          <p className="mt-2 text-xs text-slate-500">
            Register an app at developer.xero.com, then set the Client ID/secret/redirect URI before connecting.
          </p>
        )}
      </div>

      <ConfirmDialog
        open={confirmingDisconnect}
        title="Disconnect from Xero?"
        message="Mobmek will stop being connected to this Xero organisation. You can reconnect at any time."
        confirmLabel="Disconnect"
        onConfirm={disconnect}
        onCancel={() => setConfirmingDisconnect(false)}
      />
    </section>
  )
}
