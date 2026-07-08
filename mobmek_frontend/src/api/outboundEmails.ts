import { apiGet, apiPost, apiUrl } from './client'
import type { OutboundEmail, OutboundEmailFilters, OutboundEmailPage } from '@/types'

export const getOutboundEmailsPaged = (filters: OutboundEmailFilters = {}) => {
  const params = new URLSearchParams()
  if (filters.customerId) params.set('customerId', filters.customerId)
  if (filters.invoiceId) params.set('invoiceId', filters.invoiceId)
  if (filters.status) params.set('status', filters.status)
  if (filters.kind) params.set('kind', filters.kind)
  if (filters.page) params.set('page', String(filters.page))
  if (filters.pageSize) params.set('pageSize', String(filters.pageSize))
  const query = params.toString()
  return apiGet<OutboundEmailPage>(`/outbound-emails${query ? `?${query}` : ''}`)
}

export const retryOutboundEmail = (id: string) => apiPost<OutboundEmail>(`/outbound-emails/${id}/retry`, undefined)

/** URL for the raw-HTML preview — open it directly (`window.open`), don't fetch it as JSON. */
export const previewOutboundEmailUrl = (id: string) => apiUrl(`/outbound-emails/${id}/preview`)
