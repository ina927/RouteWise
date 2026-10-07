import type { Journey } from '../../contracts/journeys'
import type { RoutePoint } from '../../contracts/places'
import {
  emptyPlace,
  initialDraft,
  type PlaceDraft,
  type PlanningDraft,
} from './planning'

export function savedJourneyDraft(journey: Journey): PlanningDraft {
  const { route, travelMode, objective, endMode } = journey.plan
  const first = route.points[0]
  const last = route.points.at(-1)
  if (!first || !last || route.points.length < 2)
    throw new Error('This journey has no editable route.')
  const row = (point: RoutePoint): PlaceDraft => ({
    id: point.id,
    query: point.place.name,
    place: structuredClone(point.place),
  })
  return {
    ...initialDraft(),
    travelMode,
    objective,
    endMode,
    start: row(first),
    destinations: (endMode === 'open'
      ? route.points.slice(1)
      : route.points.slice(1, -1)
    ).map(row),
    end: endMode === 'fixed' ? row(last) : emptyPlace(),
    returnPointId: endMode === 'returnToStart' ? last.id : crypto.randomUUID(),
  }
}
