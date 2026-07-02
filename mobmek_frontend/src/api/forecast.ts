import { apiGet } from './client'
import type { ForecastResult } from '@/types'

export const getForecast = (horizonDays: number, scenario: string) =>
  apiGet<ForecastResult>(`/cashflow/forecast?horizonDays=${horizonDays}&scenario=${scenario}`)
