import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { Car, CreateCarRequest, UpdateCarRequest } from '@/types'

export const getCars = (customerId?: string) =>
  apiGet<Car[]>(`/cars${customerId ? `?customerId=${encodeURIComponent(customerId)}` : ''}`)
export const createCar = (body: CreateCarRequest) => apiPost<Car>('/cars', body)
export const updateCar = (id: string, body: UpdateCarRequest) => apiPut<Car>(`/cars/${id}`, body)
export const deleteCar = (id: string) => apiDelete(`/cars/${id}`)
