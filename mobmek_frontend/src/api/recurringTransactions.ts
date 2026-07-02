import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { CashTransaction, DueOccurrence, RecurringTransaction, RecurringTransactionRequest } from '@/types'

export const getRecurringTransactions = (includePaused = true) =>
  apiGet<RecurringTransaction[]>(`/recurring-transactions?includePaused=${includePaused}`)

export const getDueOccurrences = (autoPostOnly = false) =>
  apiGet<DueOccurrence[]>(`/recurring-transactions/due?autoPostOnly=${autoPostOnly}`)

export const getRecurringTransaction = (id: string) => apiGet<RecurringTransaction>(`/recurring-transactions/${id}`)

export const createRecurringTransaction = (body: RecurringTransactionRequest) =>
  apiPost<RecurringTransaction>('/recurring-transactions', body)

export const updateRecurringTransaction = (id: string, body: RecurringTransactionRequest) =>
  apiPut<RecurringTransaction>(`/recurring-transactions/${id}`, body)

export const deleteRecurringTransaction = (id: string) => apiDelete(`/recurring-transactions/${id}`)

export const setRecurringTransactionPaused = (id: string, paused: boolean) =>
  apiPost<RecurringTransaction>(`/recurring-transactions/${id}/pause?paused=${paused}`, undefined)

export const postRecurringOccurrence = (id: string, date: string) =>
  apiPost<CashTransaction>(`/recurring-transactions/${id}/post-occurrence?date=${date}`, undefined)
