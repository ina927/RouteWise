import type { AuthRequest, CurrentUser } from '../contracts/auth.ts'
import { request } from './httpClient.ts'

export function getCurrentUser(signal?: AbortSignal) {
  return request<CurrentUser>('/auth/me', { signal })
}
export function login(body: AuthRequest) {
  return request<CurrentUser>('/auth/login', { method: 'POST', body })
}
export function signup(body: AuthRequest) {
  return request<CurrentUser>('/auth/signup', { method: 'POST', body })
}
export function logout() {
  return request('/auth/logout', { method: 'POST', noContent: true })
}
export function deleteAccount() {
  return request('/account', { method: 'DELETE', noContent: true })
}
