import { apiGet, apiPut } from './client'
import type { CashFlowSettings, UpdateCashFlowSettingsRequest } from '@/types'

export const getCashFlowSettings = () => apiGet<CashFlowSettings>('/cash-flow-settings')
export const updateCashFlowSettings = (body: UpdateCashFlowSettingsRequest) =>
  apiPut<CashFlowSettings>('/cash-flow-settings', body)
