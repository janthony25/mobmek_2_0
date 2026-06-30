import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { Product, ProductRequest } from '@/types'

export const getProducts = () => apiGet<Product[]>('/products')
export const createProduct = (body: ProductRequest) => apiPost<Product>('/products', body)
export const updateProduct = (id: string, body: ProductRequest) =>
  apiPut<Product>(`/products/${id}`, body)
export const deleteProduct = (id: string) => apiDelete(`/products/${id}`)
