import { apiGet, apiPost, apiPut } from './client'
import type {
  AccountInvitePreview,
  AccountListItem,
  ConfirmAccountRequest,
  CreateAccountRequest,
  UpdateAccountRoleRequest,
} from '@/types'

export const getAccounts = () => apiGet<AccountListItem[]>('/accounts')
export const createAccount = (body: CreateAccountRequest) => apiPost<AccountListItem>('/accounts', body)
export const updateAccountRole = (userId: string, body: UpdateAccountRoleRequest) =>
  apiPut<AccountListItem>(`/accounts/${userId}/role`, body)
export const deactivateAccount = (userId: string) => apiPost<AccountListItem>(`/accounts/${userId}/deactivate`, {})
export const reactivateAccount = (userId: string) => apiPost<AccountListItem>(`/accounts/${userId}/reactivate`, {})

/** Public — reached from the emailed invite link, before the account can sign in at all. */
export const getInvitePreview = (token: string) =>
  apiGet<AccountInvitePreview>(`/accounts/confirm/${encodeURIComponent(token)}`)

/** Public — a brand-new account has no session yet, so this can't require one. */
export const confirmAccount = (body: ConfirmAccountRequest) => apiPost<void>('/accounts/confirm', body)
