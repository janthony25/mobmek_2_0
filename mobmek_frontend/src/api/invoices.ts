import { apiGet, apiPost } from './client'
import type {
  AcceptQuotationRequest,
  CreateInvoiceRequest,
  Invoice,
  InvoiceListItem,
  MarkInvoicePaidRequest,
  PagedResult,
} from '@/types'

const base = (jobId: string) => `/jobs/${encodeURIComponent(jobId)}/invoices`

/** One page of invoices or quotations across all jobs, for the global list pages. */
export const getInvoicesPaged = (
  documentType: 'Invoice' | 'Quotation',
  page: number,
  pageSize: number,
  search: string,
) => {
  const params = new URLSearchParams({ documentType, page: String(page), pageSize: String(pageSize) })
  if (search) params.set('search', search)
  return apiGet<PagedResult<InvoiceListItem>>(`/invoices/paged?${params.toString()}`)
}

export const getInvoices = (jobId: string) => apiGet<Invoice[]>(base(jobId))
export const getInvoice = (jobId: string, id: string) =>
  apiGet<Invoice>(`${base(jobId)}/${id}`)
export const generateInvoice = (jobId: string, body: CreateInvoiceRequest) =>
  apiPost<Invoice>(base(jobId), body)
export const generateQuotation = (jobId: string, body: CreateInvoiceRequest) =>
  apiPost<Invoice>(`${base(jobId)}/quotation`, body)
export const acceptQuotation = (jobId: string, id: string, body: AcceptQuotationRequest) =>
  apiPost<Invoice>(`${base(jobId)}/${id}/accept`, body)
export const rejectInvoice = (jobId: string, id: string) =>
  apiPost<Invoice>(`${base(jobId)}/${id}/reject`, {})
export const markInvoicePaid = (jobId: string, id: string, body: MarkInvoicePaidRequest) =>
  apiPost<Invoice>(`${base(jobId)}/${id}/pay`, body)
