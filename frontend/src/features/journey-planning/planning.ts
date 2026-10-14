import type { Place, RoutePoint } from '../../contracts/places.ts'
import type {
  EndMode,
  OptimisationObjective,
  OptimizeRouteRequest,
  RouteResult,
  TravelMode,
} from '../../contracts/routes.ts'

export type PlaceDraft = { id: string; query: string; place: Place | null }
export type PlanningDraft = {
  travelMode: TravelMode | null
  objective: OptimisationObjective | null
  endMode: EndMode | null
  start: PlaceDraft
  destinations: PlaceDraft[]
  end: PlaceDraft
  returnPointId: string
}
export type PlanningResult = {
  request: OptimizeRouteRequest
  optimisedRoute: RouteResult
  currentRoute: RouteResult
  modified: boolean
}

export function emptyPlace(): PlaceDraft {
  return { id: crypto.randomUUID(), query: '', place: null }
}

export function initialDraft(): PlanningDraft {
  return {
    travelMode: null,
    objective: null,
    endMode: null,
    start: emptyPlace(),
    destinations: [emptyPlace()],
    end: emptyPlace(),
    // A return is a second occurrence of Start, not its physical place ID.
    returnPointId: crypto.randomUUID(),
  }
}

export function locationCount(draft: PlanningDraft): number {
  return (
    Number(Boolean(draft.start.place)) +
    draft.destinations.filter((row) => row.place).length +
    (draft.endMode === 'fixed' ? Number(Boolean(draft.end.place)) : 0) +
    (draft.endMode === 'returnToStart' ? Number(Boolean(draft.start.place)) : 0)
  )
}

export function canSelectPlace(draft: PlanningDraft, id: string): boolean {
  const row = [draft.start, ...draft.destinations, draft.end].find(
    (row) => row.id === id,
  )
  if (!row) return false
  if (row.place) return true
  const cost =
    id === draft.start.id && draft.endMode === 'returnToStart' ? 2 : 1
  return locationCount(draft) + cost <= 10
}

export function canAddRow(draft: PlanningDraft): boolean {
  // Reserve eventual endpoint slots for the active mode; empty rows themselves
  // are not counted by locationCount or sent to the API.
  const endpoints =
    draft.endMode === 'fixed' || draft.endMode === 'returnToStart' ? 2 : 1
  return draft.destinations.length < 10 - endpoints && locationCount(draft) < 10
}

export function planningError(draft: PlanningDraft): string | undefined {
  const destinations = draft.destinations.flatMap((row) =>
    row.place ? [row.place] : [],
  )
  const end = draft.endMode === 'fixed' ? draft.end.place : null
  if (end && destinations.some((place) => place.placeId === end.placeId))
    return 'Remove this place from Places to visit to use it as your fixed end.'
  if (end && end.placeId === draft.start.place?.placeId)
    return 'Choose Return to start to finish at your starting place.'
  if (
    destinations.some((place) => place.placeId === draft.start.place?.placeId)
  )
    return 'Remove your starting place from Places to visit.'
  if (
    new Set(destinations.map((place) => place.placeId)).size !==
    destinations.length
  )
    return 'Remove duplicate places from Places to visit.'
  if (draft.destinations.some((row) => row.query.trim() && !row.place))
    return 'Select a search result or clear the unfinished search.'
  if (
    draft.endMode === 'returnToStart' &&
    draft.start.place &&
    !destinations.length
  )
    return 'Add at least one place to visit before returning to the start.'
  return undefined
}

export function buildRequest(
  draft: PlanningDraft,
): OptimizeRouteRequest | null {
  if (
    !draft.travelMode ||
    !draft.objective ||
    !draft.endMode ||
    !draft.start.place ||
    locationCount(draft) > 10 ||
    planningError(draft)
  )
    return null
  const destinations: RoutePoint[] = draft.destinations.flatMap((row) =>
    row.place ? [{ id: row.id, place: row.place }] : [],
  )
  const common = {
    start: { id: draft.start.id, place: draft.start.place },
    destinations,
    travelMode: draft.travelMode,
    objective: draft.objective,
  }
  if (draft.endMode === 'open')
    return destinations.length ? { ...common, endMode: 'open' } : null
  if (draft.endMode === 'fixed')
    return draft.end.place
      ? {
          ...common,
          endMode: 'fixed',
          end: { id: draft.end.id, place: draft.end.place },
        }
      : null
  return {
    ...common,
    endMode: 'returnToStart',
    returnPointId: draft.returnPointId,
  }
}

export function replacePlace(
  draft: PlanningDraft,
  id: string,
  query: string,
  place: Place | null,
): PlanningDraft {
  if (place && !canSelectPlace(draft, id)) return draft
  const update = (row: PlaceDraft) =>
    row.id === id ? { ...row, query, place } : row
  return {
    ...draft,
    start: update(draft.start),
    end: update(draft.end),
    destinations: draft.destinations.map(update),
  }
}
