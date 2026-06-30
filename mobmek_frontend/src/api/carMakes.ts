import { apiGet } from './client'
import type { CarMake } from '@/types'

export function getCarMakes(): Promise<CarMake[]> {
  return apiGet<CarMake[]>('/carmakes')
}
