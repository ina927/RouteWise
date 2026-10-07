import { createContext, useContext } from 'react'
import type { UseQueryResult } from '@tanstack/react-query'
import type { CurrentUser } from '../../contracts/auth'

export const AuthContext = createContext<{
  session: UseQueryResult<CurrentUser | null, Error>
  setSession: (
    user: CurrentUser | null,
    destination?: '/login' | '/signup',
  ) => Promise<void>
  signedOutDestination: '/login' | '/signup'
  resetDestination: () => void
  refreshSession: () => Promise<void>
} | null>(null)

export function useAuth() {
  const value = useContext(AuthContext)
  if (!value) throw new Error('AuthProvider is required.')
  return value
}
