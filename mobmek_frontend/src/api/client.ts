// Thin fetch wrapper used by all API modules.
//
// Requests are made against a relative base URL ("/api" by default) so that in
// development the Vite proxy and in production the reverse proxy forward them to
// the backend, keeping the browser on a single origin. Override with VITE_API_BASE_URL.

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api'

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

export async function apiGet<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    headers: { Accept: 'application/json' },
    ...init,
  })

  if (!response.ok) {
    throw new ApiError(response.status, `Request to ${path} failed (${response.status})`)
  }

  // 204 No Content has an empty body.
  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}
