import { apiGet, apiPost, apiPut } from './client'
import type { XeroAccount, XeroConnectResponse, XeroStatus } from '@/types'

export const getXeroStatus = () => apiGet<XeroStatus>('/integrations/xero/status')

/** Returns the Xero consent URL — the caller does a full-page navigation to it, not a fetch. */
export const connectXero = () => apiPost<XeroConnectResponse>('/integrations/xero/connect', {})

export const completeXeroConnection = (code: string, state: string) =>
  apiPost<XeroStatus>('/integrations/xero/complete', { code, state })

export const disconnectXero = () => apiPost<XeroStatus>('/integrations/xero/disconnect', {})

export const getXeroAccounts = () => apiGet<XeroAccount[]>('/integrations/xero/accounts')

export const setXeroDefaultAccountCode = (code: string | null) =>
  apiPut<XeroStatus>('/integrations/xero/default-account-code', { code })
