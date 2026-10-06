import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { CreateRoleRequest, Role, SetRolePermissionsRequest } from '@/types'

export const getRoles = () => apiGet<Role[]>('/roles')
export const getPermissionCatalog = () => apiGet<string[]>('/roles/permissions')
export const createRole = (body: CreateRoleRequest) => apiPost<Role>('/roles', body)
export const setRolePermissions = (roleId: string, body: SetRolePermissionsRequest) =>
  apiPut<Role>(`/roles/${roleId}/permissions`, body)
export const deleteRole = (roleId: string) => apiDelete(`/roles/${roleId}`)
