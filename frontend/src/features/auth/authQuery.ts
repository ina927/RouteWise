import { queryOptions } from '@tanstack/react-query'
import { getCurrentUser } from '../../api/authApi'
import { ApiError } from '../../api/httpClient'

export const currentUserKey = ['current-user'] as const
export const currentUserOptions = queryOptions({
  queryKey: currentUserKey,
  queryFn: async ({ signal }) => {
    try {
      return await getCurrentUser(signal)
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) return null
      throw error
    }
  },
  retry: false,
  staleTime: 30_000,
})
