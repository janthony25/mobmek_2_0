import { apiDelete, apiGet, apiPost, apiPostForm, apiPut, apiUrl } from './client'
import type {
  BulkTransactionRequest,
  BulkTransactionResult,
  CashTransaction,
  CashTransactionPage,
  CashTransactionRequest,
  CreateTransferRequest,
  SplitTransactionRequest,
  TransactionAttachment,
} from '@/types'

export interface CashTransactionQuery {
  accountId?: string
  categoryId?: string
  payeeId?: string
  direction?: string
  status?: string
  from?: string
  to?: string
  search?: string
  splitGroupId?: string
  page?: number
  pageSize?: number
}

const queryString = (query: CashTransactionQuery) => {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== '') params.set(key, String(value))
  }
  const qs = params.toString()
  return qs ? `?${qs}` : ''
}

export const getCashTransactions = (query: CashTransactionQuery = {}) =>
  apiGet<CashTransactionPage>(`/cash-transactions${queryString(query)}`)

/** CSV download URL for the current filter; hand it to an <a href>. */
export const cashTransactionsExportUrl = (query: CashTransactionQuery = {}) =>
  apiUrl(`/cash-transactions/export${queryString(query)}`)

export const getCashTransaction = (id: string) => apiGet<CashTransaction>(`/cash-transactions/${id}`)
export const createCashTransaction = (body: CashTransactionRequest) =>
  apiPost<CashTransaction>('/cash-transactions', body)
export const updateCashTransaction = (id: string, body: CashTransactionRequest) =>
  apiPut<CashTransaction>(`/cash-transactions/${id}`, body)
export const deleteCashTransaction = (id: string) => apiDelete(`/cash-transactions/${id}`)

export const createTransfer = (body: CreateTransferRequest) =>
  apiPost<CashTransaction[]>('/cash-transactions/transfer', body)

export const createSplitTransaction = (body: SplitTransactionRequest) =>
  apiPost<CashTransaction[]>('/cash-transactions/split', body)
export const updateSplitTransaction = (splitGroupId: string, body: SplitTransactionRequest) =>
  apiPut<CashTransaction[]>(`/cash-transactions/split/${splitGroupId}`, body)

/** Applies one action to many rows; protected rows come back in skipped with reasons. */
export const bulkCashTransactions = (body: BulkTransactionRequest) =>
  apiPost<BulkTransactionResult>('/cash-transactions/bulk', body)

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
