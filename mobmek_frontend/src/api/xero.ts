import { apiGet, apiPost } from './client'
import type { XeroConnectResponse, XeroStatus } from '@/types'

export const getXeroStatus = () => apiGet<XeroStatus>('/integrations/xero/status')

/** Returns the Xero consent URL — the caller does a full-page navigation to it, not a fetch. */
export const connectXero = () => apiPost<XeroConnectResponse>('/integrations/xero/connect', {})

export const completeXeroConnection = (code: string, state: string) =>
  apiPost<XeroStatus>('/integrations/xero/complete', { code, state })

export const disconnectXero = () => apiPost<XeroStatus>('/integrations/xero/disconnect', {})
