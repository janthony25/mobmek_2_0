import { useEffect, useState } from 'react'
import { connectXero, disconnectXero, getXeroAccounts, getXeroStatus, setXeroDefaultAccountCode } from '@/api/xero'
import { ApiError } from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { StateMessage } from '@/components/ui/StateMessage'
import { controlClass } from '@/components/forms/controls'
import { useToast } from '@/components/ui/toast'
import { useAsync } from '@/hooks/useAsync'
import { dateTime, orDash } from '@/lib/format'

/**
 * Connect/disconnect, status, and the default sales account-code mapping for the Xero
 * integration (docs/xero-integration-design.md §6). Phase 1 only pushes invoices once this page
 * shows Connected and an account code is picked — that's the one piece of Chart-of-Accounts
 * mapping the push job needs.
 */
export function XeroSettingsPage() {
  const toast = useToast()
  const { data, loading, error, reload } = useAsync(getXeroStatus, [])
  const { data: accounts } = useAsync(() => (data?.connected ? getXeroAccounts() : Promise.resolve([])), [data?.connected])
  const [connecting, setConnecting] = useState(false)
  const [disconnecting, setDisconnecting] = useState(false)
  const [confirmingDisconnect, setConfirmingDisconnect] = useState(false)
  const [accountCode, setAccountCode] = useState('')
  const [savingAccountCode, setSavingAccountCode] = useState(false)

  useEffect(() => {
    setAccountCode(data?.defaultSalesAccountCode ?? '')
  }, [data?.defaultSalesAccountCode])

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

  const saveAccountCode = async () => {
    setSavingAccountCode(true)
    try {
      await setXeroDefaultAccountCode(accountCode || null)
      toast.success('Default sales account updated.')
      reload()
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Failed to save the default sales account.')
    } finally {
      setSavingAccountCode(false)
    }
  }

  const statusTone = data?.status === 'Connected' ? 'green' : data?.status === 'Error' ? 'red' : 'amber'

  return (
    <section className="max-w-xl">
      <h1 className="text-2xl font-semibold text-slate-900">Xero</h1>
      <p className="mt-1 text-sm text-slate-500">
        Connects this app to your Xero organisation for bookkeeping/reconciliation visibility. Once connected and a
        default sales account is picked below, generated invoices push to Xero automatically.
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

      {data?.connected && (
        <div className="mt-6 rounded-lg border border-slate-200 bg-white p-5">
          <h2 className="text-sm font-medium text-slate-700">Default sales account</h2>
          <p className="mt-1 text-xs text-slate-500">
            The Xero account invoice lines post against — required before invoices can push to Xero.
          </p>

          <div className="mt-3 flex items-end gap-3">
            <select
              className={`${controlClass} mt-0 max-w-xs`}
              value={accountCode}
              onChange={(event) => setAccountCode(event.target.value)}
            >
              <option value="">— Select an account —</option>
              {(accounts ?? []).map((account) => (
                <option key={account.code} value={account.code}>
                  {account.code} — {account.name}
                </option>
              ))}
            </select>
            <Button
              onClick={saveAccountCode}
              disabled={savingAccountCode || accountCode === (data.defaultSalesAccountCode ?? '')}
            >
              {savingAccountCode ? 'Saving…' : 'Save'}
            </Button>
          </div>
        </div>
      )}

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
