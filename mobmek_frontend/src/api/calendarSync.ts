import { apiGet, apiPost } from './client'
import type { CalendarSyncStatus } from '@/types'

export const getCalendarSyncStatus = () => apiGet<CalendarSyncStatus>('/calendarsync/status')

/** Runs the reconcile pass now instead of waiting for the hourly tick. */
export const reconcileCalendarSync = (backfill = false) =>
  apiPost<CalendarSyncStatus>(`/calendarsync/reconcile?backfill=${backfill}`, {})
