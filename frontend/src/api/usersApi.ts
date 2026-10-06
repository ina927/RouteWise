import type { UserSummary } from '../contracts/users.ts'
import { request } from './httpClient.ts'

export function searchUsers(query: string, signal?: AbortSignal) {
  return request<UserSummary[]>(`/users?${new URLSearchParams({ query })}`, {
    signal,
  })
}
