import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { CarModel } from '@/types'

interface CarModelRequest {
  carMakeId: string
  name: string
}

export const getCarModels = (makeId?: string) =>
  apiGet<CarModel[]>(`/carmodels${makeId ? `?makeId=${encodeURIComponent(makeId)}` : ''}`)
export const createCarModel = (body: CarModelRequest) => apiPost<CarModel>('/carmodels', body)
export const updateCarModel = (id: string, body: CarModelRequest) =>
  apiPut<CarModel>(`/carmodels/${id}`, body)
export const deleteCarModel = (id: string) => apiDelete(`/carmodels/${id}`)
