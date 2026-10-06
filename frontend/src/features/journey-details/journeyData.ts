import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useRef } from 'react'
import { updateJourney } from '../../api/journeysApi'
import type {
  Journey,
  PointAnnotation,
  UpdateJourneyRequest,
} from '../../contracts/journeys'

export const journeyKey = (id: string) => ['journey', id] as const

export function replaceAnnotationField(
  annotations: PointAnnotation[],
  pointId: string,
  field: 'note' | 'plannedTime',
  value: string,
): PointAnnotation[] {
  const next = {
    ...annotations.find((item) => item.pointId === pointId),
    pointId,
  }
  if (value) next[field] = value
  else delete next[field]
  const others = annotations.filter((item) => item.pointId !== pointId)
  return next.note || next.plannedTime ? [...others, next] : others
}

export function useJourneyUpdate(journey: Journey) {
  const client = useQueryClient()
  const busy = useRef(false)
  const mutation = useMutation({
    mutationFn: (body: UpdateJourneyRequest) => updateJourney(journey.id, body),
  })
  async function save(build: (current: Journey) => UpdateJourneyRequest) {
    if (busy.current || journey.access !== 'owner') return false
    busy.current = true
    try {
      // Annotation patches replace the whole array: always merge against the latest response.
      await client.cancelQueries({ queryKey: journeyKey(journey.id) })
      const current =
        client.getQueryData<Journey>(journeyKey(journey.id)) ?? journey
      if (current.access !== 'owner') return false
      const updated = await mutation.mutateAsync(build(current))
      client.setQueryData(journeyKey(journey.id), updated)
      return true
    } catch {
      return false
    } finally {
      busy.current = false
    }
  }
  return {
    save,
    pending: mutation.isPending,
    error: mutation.error,
    reset: mutation.reset,
  }
}
