import { useCallback, useState, type ReactNode } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import type { CurrentUser } from '../../contracts/auth'
import { AuthContext } from './authContext'

import { currentUserKey, currentUserOptions } from './authQuery'

export function AuthProvider({ children }: { children: ReactNode }) {
  const client = useQueryClient()
  const session = useQuery(currentUserOptions)
  const [signedOutDestination, setDestination] = useState<'/login' | '/signup'>(
    '/login',
  )
  const resetDestination = useCallback(() => setDestination('/login'), [])
  const setSession = useCallback(
    async (
      user: CurrentUser | null,
      destination: '/login' | '/signup' = '/login',
    ) => {
      await client.cancelQueries({ queryKey: currentUserKey })
      setDestination(destination)
      client.setQueryData(currentUserKey, user)
    },
    [client],
  )
  const refreshSession = useCallback(async () => {
    await client.invalidateQueries({ queryKey: currentUserKey })
  }, [client])
  return (
    <AuthContext
      value={{
        session,
        setSession,
        refreshSession,
        signedOutDestination,
        resetDestination,
      }}
    >
      {children}
    </AuthContext>
  )
}
