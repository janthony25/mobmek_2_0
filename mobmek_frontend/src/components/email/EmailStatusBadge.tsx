import { Badge } from '@/components/ui/Badge'
import type { Tone } from '@/components/ui/Badge'
import type { OutboundEmailStatus } from '@/types'

const LABELS: Record<OutboundEmailStatus, string> = {
  Queued: 'Sending…',
  Sent: 'Sent',
  Delivered: 'Delivered ✓',
  Bounced: 'Bounced ✗',
  Complained: 'Marked as spam',
  Failed: 'Failed ✗',
}

const TONES: Record<OutboundEmailStatus, Tone> = {
  Queued: 'slate',
  Sent: 'blue',
  Delivered: 'green',
  Bounced: 'red',
  Complained: 'orange',
  Failed: 'red',
}

interface EmailStatusBadgeProps {
  status: OutboundEmailStatus | null
  errorMessage?: string | null
}

/** Delivery-status pill for an invoice's most recent email, or a dash if never emailed. */
export function EmailStatusBadge({ status, errorMessage }: EmailStatusBadgeProps) {
  if (status === null) {
    return <span className="text-sm text-slate-400">—</span>
  }

  return (
    <span title={errorMessage ?? undefined}>
      <Badge tone={TONES[status]}>{LABELS[status]}</Badge>
    </span>
  )
}
