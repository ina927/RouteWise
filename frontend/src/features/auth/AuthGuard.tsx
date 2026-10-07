import { useEffect, useRef, useState } from 'react'
import {
  MutationCache,
  QueryCache,
  QueryClient,
  QueryClientProvider,
  useQuery,
} from '@tanstack/react-query'
import { Navigate, Outlet } from 'react-router'
import { ApiError } from '../../api/httpClient'
import { Button } from '../../components/ui/Button'
import { useAuth } from './authContext'
import { currentUserOptions } from './authQuery'
import styles from './Auth.module.css'

export function AuthGuard({ publicOnly = false }: { publicOnly?: boolean }) {
  // Read the cache on route render, including immediately after a session mutation.
  const session = useQuery(currentUserOptions)
  const { signedOutDestination, resetDestination } = useAuth()
  useEffect(() => {
    if (publicOnly && session.data === null) resetDestination()
  }, [publicOnly, session.data, resetDestination])
  if (session.isPending)
    return (
      <main className={styles.standalone}>
        <p role="status">Checking your session…</p>
      </main>
    )
  if (session.isError)
    return (
      <main className={styles.standalone}>
        <div className={styles.form}>
          <h1>Unable to check your session</h1>
          <p role="alert">Please try again.</p>
          <Button
            onClick={() => {
              void session.refetch()
            }}
          >
            Retry
          </Button>
        </div>
      </main>
    )
  if (publicOnly)
    return session.data ? <Navigate to="/plan" replace /> : <Outlet />
  if (!session.data) return <Navigate to={signedOutDestination} replace />
  return <ProductSession key={session.data.id} />
}

function ProductSession() {
  const { refreshSession } = useAuth()
  const [client] = useState(() => {
    const onError = (error: Error) => {
      if (error instanceof ApiError && error.status === 401)
        void refreshSession()
    }
    return new QueryClient({
      queryCache: new QueryCache({ onError }),
      mutationCache: new MutationCache({ onError }),
    })
  })
  const mounted = useRef(false)
  useEffect(() => {
    mounted.current = true
    return () => {
      mounted.current = false
      // StrictMode replays effects; dispose only after a genuine unmount.
      queueMicrotask(() => {
        if (!mounted.current) {
          void client.cancelQueries()
          client.clear()
        }
      })
    }
  }, [client])
  // Each authenticated mount has its own cache. Late callbacks from an old
  // session cannot populate a later user's client, including a same-user login.
  return (
    <QueryClientProvider client={client}>
      <Outlet />
    </QueryClientProvider>
  )
}
