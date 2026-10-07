const configuredBase = import.meta.env?.VITE_API_BASE_URL?.trim() || '/api'

export function apiUrl(path: string): string {
  return new URL(
    `${configuredBase.replace(/\/+$/, '')}/${path.replace(/^\/+/, '')}`,
    globalThis.location.origin,
  ).href
}
