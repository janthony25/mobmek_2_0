// Thin fetch wrapper used by all API modules.
//
// Requests are made against a relative base URL ("/api" by default) so that in
// development the Vite proxy and in production the reverse proxy forward them to
// the backend, keeping the browser on a single origin. Override with VITE_API_BASE_URL.

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api'

// AuthContext registers a handler here on mount so any 401 response (session expired,
// never logged in) clears local auth state, regardless of which API module triggered it.
let unauthorizedHandler: (() => void) | null = null

export function setUnauthorizedHandler(handler: (() => void) | null): void {
  unauthorizedHandler = handler
}

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

/** Pulls a human-readable message out of an ASP.NET ProblemDetails / validation body. */
async function toApiError(response: Response, path: string): Promise<ApiError> {
  try {
    const body = await response.json()

    // Validation problem details: { errors: { Field: ["msg", ...] } }
    if (body?.errors && typeof body.errors === 'object') {
      const messages = Object.values(body.errors as Record<string, string[]>)
        .flat()
        .filter(Boolean)
      if (messages.length > 0) {
        return new ApiError(response.status, messages.join(' '))
      }
    }

    const message = body?.detail ?? body?.title
    if (typeof message === 'string') {
      return new ApiError(response.status, message)
    }
  } catch {
    // Body was empty or not JSON — fall through to a generic message.
  }

  return new ApiError(response.status, `Request to ${path} failed (${response.status})`)
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    method,
    credentials: 'include',
    headers: {
      Accept: 'application/json',
      ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
    },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  })

  if (!response.ok) {
    if (response.status === 401) unauthorizedHandler?.()
    throw await toApiError(response, path)
  }

  // 204 No Content (and empty bodies) have nothing to parse.
  if (response.status === 204 || response.headers.get('Content-Length') === '0') {
    return undefined as T
  }

  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export const apiGet = <T>(path: string): Promise<T> => request<T>('GET', path)
export const apiPost = <T>(path: string, body: unknown): Promise<T> => request<T>('POST', path, body)
export const apiPut = <T>(path: string, body: unknown): Promise<T> => request<T>('PUT', path, body)
export const apiDelete = (path: string): Promise<void> => request<void>('DELETE', path)

/** POSTs a multipart form (file uploads); the browser sets the Content-Type boundary itself. */
export async function apiPostForm<T>(path: string, form: FormData): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    method: 'POST',
    credentials: 'include',
    headers: { Accept: 'application/json' },
    body: form,
  })

  if (!response.ok) {
    if (response.status === 401) unauthorizedHandler?.()
    throw await toApiError(response, path)
  }

  return (await response.json()) as T
}

/** Absolute-ish URL for links the browser fetches itself (file downloads). */
export const apiUrl = (path: string): string => `${BASE_URL}${path}`
