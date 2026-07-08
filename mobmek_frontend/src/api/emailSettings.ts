import { apiGet, apiPost, apiPut } from './client'
import type { EmailSettings, OutboundEmail, UpdateEmailSettingsRequest } from '@/types'

export const getEmailSettings = () => apiGet<EmailSettings>('/emailsettings')
export const updateEmailSettings = (body: UpdateEmailSettingsRequest) =>
  apiPut<EmailSettings>('/emailsettings', body)
export const sendTestEmail = (toAddress: string) =>
  apiPost<OutboundEmail>('/emailsettings/test', { toAddress })
