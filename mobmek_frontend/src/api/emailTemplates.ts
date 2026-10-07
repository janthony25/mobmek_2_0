import { apiGet, apiPost, apiPut } from './client'
import type { EmailTemplate, EmailTemplatePreview, UpdateEmailTemplateRequest } from '@/types'

export const getEmailTemplates = () => apiGet<EmailTemplate[]>('/email-templates')
export const updateEmailTemplate = (key: string, body: UpdateEmailTemplateRequest) =>
  apiPut<EmailTemplate>(`/email-templates/${key}`, body)
export const previewEmailTemplate = (key: string) =>
  apiPost<EmailTemplatePreview>(`/email-templates/${key}/preview`, undefined)
