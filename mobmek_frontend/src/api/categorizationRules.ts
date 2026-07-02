import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { ApplyRuleResult, CategorizationRule, CategorizationRuleRequest, RuleSuggestion } from '@/types'

export const getCategorizationRules = () => apiGet<CategorizationRule[]>('/categorization-rules')
export const createCategorizationRule = (body: CategorizationRuleRequest) =>
  apiPost<CategorizationRule>('/categorization-rules', body)
export const updateCategorizationRule = (id: string, body: CategorizationRuleRequest) =>
  apiPut<CategorizationRule>(`/categorization-rules/${id}`, body)
export const deleteCategorizationRule = (id: string) => apiDelete(`/categorization-rules/${id}`)

/** The winning rule's pre-fill for what's been typed so far; null when nothing matches. */
export const suggestCategorization = (body: {
  description: string | null
  counterparty: string | null
  direction: string | null
  amount: number | null
}) => apiPost<RuleSuggestion | null>('/categorization-rules/suggest', body)

/** commit=false previews the match/update counts without changing anything. */
export const applyRuleToExisting = (id: string, commit: boolean) =>
  apiPost<ApplyRuleResult>(`/categorization-rules/${id}/apply-to-existing?commit=${commit}`, {})
