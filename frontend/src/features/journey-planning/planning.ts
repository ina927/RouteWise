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
    (draft.endMode === 'fixed' ? Number(Boolean(draft.end.place)) : 0)
  )
}

export function canSelectPlace(draft: PlanningDraft, id: string): boolean {
  const row = [draft.start, ...draft.destinations, draft.end].find(
    (row) => row.id === id,
  )

  if (!row) return false
  if (row.place) return true

  return locationCount(draft) + 1 <= 10
}

export function canAddRow(draft: PlanningDraft): boolean {
  // reserve a slot for start and another for a fixed end
  const endpoints = draft.endMode === 'fixed' ? 2 : 1

  return draft.destinations.length < 10 - endpoints && locationCount(draft) < 10
}

export function buildRequest(
  draft: PlanningDraft,
): OptimizeRouteRequest | null {
  if (
    !draft.travelMode ||
    !draft.objective ||
    !draft.endMode ||
    !draft.start.place ||
    locationCount(draft) > 10
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
