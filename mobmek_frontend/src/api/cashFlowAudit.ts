import { apiGet } from './client'
import type { CashFlowAuditPage } from '@/types'

export interface AuditQuery {
  entityType?: string
  entityId?: string
  page?: number
  pageSize?: number
}

export const getAuditTrail = (query: AuditQuery = {}) => {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== '') params.set(key, String(value))
  }
  const qs = params.toString()
  return apiGet<CashFlowAuditPage>(`/cash-flow-audit${qs ? `?${qs}` : ''}`)
}
