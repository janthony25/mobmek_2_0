import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { CashAccount, CashAccountRequest } from '@/types'

export const getCashAccounts = (includeArchived = false) =>
  apiGet<CashAccount[]>(`/cash-accounts?includeArchived=${includeArchived}`)
export const getCashAccount = (id: string) => apiGet<CashAccount>(`/cash-accounts/${id}`)
export const createCashAccount = (body: CashAccountRequest) =>
  apiPost<CashAccount>('/cash-accounts', body)
export const updateCashAccount = (id: string, body: CashAccountRequest) =>
  apiPut<CashAccount>(`/cash-accounts/${id}`, body)
export const deleteCashAccount = (id: string) => apiDelete(`/cash-accounts/${id}`)
