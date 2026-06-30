import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { CarMake, NamedLookupRequest } from '@/types'

export const getCarMakes = () => apiGet<CarMake[]>('/carmakes')
export const createCarMake = (body: NamedLookupRequest) => apiPost<CarMake>('/carmakes', body)
export const updateCarMake = (id: string, body: NamedLookupRequest) =>
  apiPut<CarMake>(`/carmakes/${id}`, body)
export const deleteCarMake = (id: string) => apiDelete(`/carmakes/${id}`)
