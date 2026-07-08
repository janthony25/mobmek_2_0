import { useState } from 'react'
import { getOutboundEmailsPaged, retryOutboundEmail, previewOutboundEmailUrl } from '@/api/outboundEmails'
import { Button } from '@/components/ui/Button'
import { Spinner } from '@/components/ui/Spinner'
import { useToast } from '@/components/ui/toast'
import { useAsync } from '@/hooks/useAsync'
import { date } from '@/lib/format'
import { EmailStatusBadge } from './EmailStatusBadge'

interface EmailHistoryListProps {
  invoiceId: string
  /** Bump to force a reload, e.g. right after a new send. */
  reloadKey?: number
}

/** Send history for one invoice — newest first, with a retry action for failed/bounced sends. */
export function EmailHistoryList({ invoiceId, reloadKey = 0 }: EmailHistoryListProps) {
  const toast = useToast()
  const [retryingId, setRetryingId] = useState<string | null>(null)
  const { data, loading, reload } = useAsync(
    () => getOutboundEmailsPaged({ invoiceId, pageSize: 10 }),
    [invoiceId, reloadKey],
  )

  const handleRetry = async (id: string) => {
    setRetryingId(id)
    try {
      await retryOutboundEmail(id)
      toast.success('Email re-sent')
      reload()
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Retry failed')
    } finally {
      setRetryingId(null)
    }
  }

  if (loading && data == null) {
    return (
      <div className="flex items-center gap-2 text-sm text-slate-400">
        <Spinner className="h-4 w-4" /> Loading history…
      </div>
    )
  }

  if (!data || data.items.length === 0) {
    return <p className="text-sm text-slate-400">Not emailed yet.</p>
  }

  return (
    <ul className="divide-y divide-slate-100 text-sm">
      {data.items.map((email) => (
        <li key={email.id} className="flex items-center justify-between gap-3 py-2">
          <div className="min-w-0">
            <p className="truncate font-medium text-slate-900">{email.toAddress}</p>
            <p className="text-xs text-slate-500">{date(email.createdAtUtc)}</p>
          </div>
          <div className="flex shrink-0 items-center gap-2">
            <EmailStatusBadge status={email.status} errorMessage={email.errorMessage} />
            <button
              type="button"
              onClick={() => window.open(previewOutboundEmailUrl(email.id), '_blank')}
              className="text-xs font-medium text-slate-500 hover:text-slate-800"
            >
              View
            </button>
            {(email.status === 'Failed' || email.status === 'Bounced') && (
              <Button
                type="button"
                size="sm"
                variant="secondary"
                disabled={retryingId === email.id}
                onClick={() => handleRetry(email.id)}
              >
                {retryingId === email.id ? 'Retrying…' : 'Retry'}
              </Button>
            )}
          </div>
        </li>
      ))}
    </ul>
  )
}
