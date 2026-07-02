import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { TransactionCategory, TransactionCategoryRequest } from '@/types'

export const getTransactionCategories = (includeArchived = false) =>
  apiGet<TransactionCategory[]>(`/transaction-categories?includeArchived=${includeArchived}`)
export const createTransactionCategory = (body: TransactionCategoryRequest) =>
  apiPost<TransactionCategory>('/transaction-categories', body)
export const updateTransactionCategory = (id: string, body: TransactionCategoryRequest) =>
  apiPut<TransactionCategory>(`/transaction-categories/${id}`, body)
export const deleteTransactionCategory = (id: string) => apiDelete(`/transaction-categories/${id}`)
