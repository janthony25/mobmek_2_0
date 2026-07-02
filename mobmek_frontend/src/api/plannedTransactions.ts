import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { CreatePlannedTransactionRequest, PlannedTransaction, UpdatePlannedTransactionRequest } from '@/types'

export const getPlannedTransactions = () => apiGet<PlannedTransaction[]>('/planned-transactions')
export const getPlannedTransaction = (id: string) => apiGet<PlannedTransaction>(`/planned-transactions/${id}`)

export const createPlannedTransaction = (body: CreatePlannedTransactionRequest) =>
  apiPost<PlannedTransaction>('/planned-transactions', body)

export const updatePlannedTransaction = (id: string, body: UpdatePlannedTransactionRequest) =>
  apiPut<PlannedTransaction>(`/planned-transactions/${id}`, body)

export const deletePlannedTransaction = (id: string) => apiDelete(`/planned-transactions/${id}`)
