import { apiUrl } from './config.ts'

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

type RequestOptions = {
  method?: 'GET' | 'POST' | 'PATCH' | 'DELETE'
  body?: unknown
  signal?: AbortSignal
  noContent?: boolean
}

export function request(
  path: string,
  options: RequestOptions & { noContent: true },
): Promise<void>
export function request<T>(
  path: string,
  options?: RequestOptions & { noContent?: false },
): Promise<T>
export async function request(
  path: string,
  options: RequestOptions = {},
): Promise<unknown> {
  const response = await fetch(apiUrl(path), {
    credentials: 'same-origin',
    method: options.method ?? 'GET',
    signal: options.signal,
    headers: {
      Accept: 'application/json',
      ...(options.body === undefined
        ? {}
        : { 'Content-Type': 'application/json' }),
    },
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
  })

  if (!response.ok) {
    let message = `Request failed (HTTP ${response.status}).`
    try {
      const body: unknown = await response.json()
      if (
        typeof body === 'object' &&
        body !== null &&
        'message' in body &&
        typeof body.message === 'string'
      ) {
        message = body.message
      }
    } catch {
      // Preserve the HTTP failure even when a proxy returns HTML or an empty body.
    }
    throw new ApiError(response.status, message)
  }

  if (options.noContent) return
  if (response.status === 204) {
    throw new Error('The API returned no content where JSON was expected.')
  }
  return response.json() as Promise<unknown>
}
