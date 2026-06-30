import { apiGet } from './client'
import type { CarModel } from '@/types'

export function getCarModels(makeId?: string): Promise<CarModel[]> {
  const query = makeId ? `?makeId=${encodeURIComponent(makeId)}` : ''
  return apiGet<CarModel[]>(`/carmodels${query}`)
}
