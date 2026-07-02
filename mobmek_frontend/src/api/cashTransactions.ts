import { apiDelete, apiGet, apiPost, apiPostForm, apiPut, apiUrl } from './client'
import type {
  CashTransaction,
  CashTransactionPage,
  CashTransactionRequest,
  CreateTransferRequest,
  TransactionAttachment,
} from '@/types'

export interface CashTransactionQuery {
  accountId?: string
  categoryId?: string
  direction?: string
  from?: string
  to?: string
  search?: string
  page?: number
  pageSize?: number
}

export const getCashTransactions = (query: CashTransactionQuery = {}) => {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== '') params.set(key, String(value))
  }
  const qs = params.toString()
  return apiGet<CashTransactionPage>(`/cash-transactions${qs ? `?${qs}` : ''}`)
}

export const getCashTransaction = (id: string) => apiGet<CashTransaction>(`/cash-transactions/${id}`)
export const createCashTransaction = (body: CashTransactionRequest) =>
  apiPost<CashTransaction>('/cash-transactions', body)
export const updateCashTransaction = (id: string, body: CashTransactionRequest) =>
  apiPut<CashTransaction>(`/cash-transactions/${id}`, body)
export const deleteCashTransaction = (id: string) => apiDelete(`/cash-transactions/${id}`)

export const createTransfer = (body: CreateTransferRequest) =>
  apiPost<CashTransaction[]>('/cash-transactions/transfer', body)

export const addTransactionAttachment = (transactionId: string, file: File) => {
  const form = new FormData()
  form.append('file', file)
  return apiPostForm<TransactionAttachment>(`/cash-transactions/${transactionId}/attachments`, form)
}

export const deleteTransactionAttachment = (transactionId: string, attachmentId: string) =>
  apiDelete(`/cash-transactions/${transactionId}/attachments/${attachmentId}`)

/** Download URL for an attachment; hand it to an <a href> so the browser streams the file. */
export const transactionAttachmentUrl = (transactionId: string, attachmentId: string) =>
  apiUrl(`/cash-transactions/${transactionId}/attachments/${attachmentId}`)
