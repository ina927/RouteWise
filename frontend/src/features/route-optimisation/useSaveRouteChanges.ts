import { useEffect, useRef } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router'
import { updateJourney } from '../../api/journeysApi'
import type { Journey, JourneyPlan } from '../../contracts/journeys'
import { cacheJourney } from '../journey-details/journeyData'

export function useSaveRouteChanges(journey: Journey | null) {
  const client = useQueryClient()
  const navigate = useNavigate()
  const guard = useRef(false)
  const mounted = useRef(true)
  useEffect(() => {
    mounted.current = true
    return () => {
      mounted.current = false
    }
  }, [])
  const mutation = useMutation({
    mutationFn: async (plan: JourneyPlan) => {
      if (!journey || journey.access !== 'owner')
        throw new Error('This journey cannot be edited.')
      const updated = await updateJourney(journey.id, { plan })
      await cacheJourney(client, updated)
      return updated
    },
  })
  async function save(plan: JourneyPlan) {
    if (guard.current || !journey) return
    guard.current = true
    try {
      const updated = await mutation.mutateAsync(structuredClone(plan))
      if (mounted.current)
        await navigate(`/journeys/${encodeURIComponent(updated.id)}`)
    } catch {
      /* Preserve the edit session and displayed result for retry. */
    } finally {
      guard.current = false
    }
  }
  return { save, pending: mutation.isPending, error: mutation.error }
}
