import { useState } from 'react'
import { getCalendarSyncStatus, reconcileCalendarSync } from '@/api/calendarSync'
import { ApiError } from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { StateMessage } from '@/components/ui/StateMessage'
import { useToast } from '@/components/ui/toast'
import { useAsync } from '@/hooks/useAsync'
import { dateTime, orDash } from '@/lib/format'

/**
 * Read-only diagnostics for the Google Calendar sync — answers "why isn't this on my phone"
 * without needing a developer to check logs (docs/google-calendar-sync-design.md §6).
 */
export function CalendarSyncSettingsPage() {
  const toast = useToast()
  const { data, loading, error, reload } = useAsync(getCalendarSyncStatus, [])
  const [syncing, setSyncing] = useState(false)

  if (loading && !data) return <StateMessage title="Loading calendar sync status…" loading />
  if (error) return <StateMessage title="Could not load calendar sync status" description={error.message} />

  const syncNow = async () => {
    setSyncing(true)
    try {
      await reconcileCalendarSync()
      toast.success('Reconcile complete.')
      reload()
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Failed to reconcile.')
    } finally {
      setSyncing(false)
    }
  }

  return (
    <section className="max-w-xl">
      <h1 className="text-2xl font-semibold text-slate-900">Calendar Sync</h1>
      <p className="mt-1 text-sm text-slate-500">
        Appointments are mirrored one-way onto the "Mobmek Workshop" Google Calendar. This page is
        read-only diagnostics — booking and editing appointments works the same either way.
      </p>

      <div className="mt-6 rounded-lg border border-slate-200 bg-white p-5">
        <div className="mb-4 flex items-center gap-2">
          <span className="text-sm font-medium text-slate-700">Status</span>
          {data?.configured ? (
            <Badge tone="green">Configured</Badge>
          ) : (
            <Badge tone="amber">Not configured — set GoogleCalendar:CredentialsJson / :CalendarId</Badge>
          )}
        </div>

        <dl className="grid grid-cols-1 gap-x-6 gap-y-3 text-sm sm:grid-cols-2">
          <div>
            <dt className="text-slate-500">Pending pushes</dt>
            <dd className="font-medium text-slate-900">{data?.outboxDepth ?? 0}</dd>
          </div>
          <div>
            <dt className="text-slate-500">Oldest pending since</dt>
            <dd className="font-medium text-slate-900">{dateTime(data?.oldestPendingUtc)}</dd>
          </div>
          <div>
            <dt className="text-slate-500">Last reconcile</dt>
            <dd className="font-medium text-slate-900">{dateTime(data?.lastReconcileUtc)}</dd>
          </div>
          <div className="sm:col-span-2">
            <dt className="text-slate-500">Last reconcile error</dt>
            <dd className={data?.lastReconcileError ? 'font-medium text-red-700' : 'font-medium text-slate-900'}>
              {orDash(data?.lastReconcileError)}
            </dd>
          </div>
        </dl>

        <div className="mt-4">
          <Button onClick={syncNow} disabled={syncing || !data?.configured}>
            {syncing ? 'Syncing…' : 'Sync now'}
          </Button>
          {!data?.configured && (
            <p className="mt-2 text-xs text-slate-500">
              Sync is disabled until the Google Cloud credentials and calendar id are configured.
            </p>
          )}
        </div>
      </div>
    </section>
  )
}
