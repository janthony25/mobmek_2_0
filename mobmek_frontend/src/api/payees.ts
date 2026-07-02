import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { Payee, PayeeRequest, PayeeSummary } from '@/types'

export const getPayees = (includeArchived = false) =>
  apiGet<Payee[]>(`/payees${includeArchived ? '?includeArchived=true' : ''}`)

export const getPayeeSummary = (id: string) => apiGet<PayeeSummary>(`/payees/${id}/summary`)
export const createPayee = (body: PayeeRequest) => apiPost<Payee>('/payees', body)
export const updatePayee = (id: string, body: PayeeRequest) => apiPut<Payee>(`/payees/${id}`, body)
export const deletePayee = (id: string) => apiDelete(`/payees/${id}`)
