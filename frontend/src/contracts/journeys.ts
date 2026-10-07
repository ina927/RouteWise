import type { Place } from './places.ts'
import type {
  EndMode,
  OptimisationObjective,
  RouteResult,
  TravelMode,
} from './routes.ts'
import type { UserSummary } from './users.ts'

export type PointAnnotation = {
  pointId: string
  note?: string
  /** Manually entered HH:mm, with no timezone or scheduling semantics. */
  plannedTime?: string
}

export type JourneyPlan = {
  travelMode: TravelMode
  objective: OptimisationObjective
  endMode: EndMode
  route: RouteResult
}

export type Journey = {
  id: string
  owner: UserSummary
  name: string
  /** Calendar date YYYY-MM-DD; metadata only. */
  date?: string
  plan: JourneyPlan
  annotations: PointAnnotation[]
  access: 'owner' | 'shared'
}

export type JourneySummary = {
  id: string
  owner: UserSummary
  name: string
  date?: string
  /** Google place ID for later card photo lookup; selection policy is undecided. */
  representativePlaceId: string
  travelMode: TravelMode
  start: Pick<Place, 'placeId' | 'name'>
  end: Pick<Place, 'placeId' | 'name'>
  locationCount: number
  totalDistanceMeters: number
  totalDurationSeconds: number
}

export type CreateJourneyRequest = {
  name: string
  date?: string
  plan: JourneyPlan
  annotations?: PointAnnotation[]
}

export type UpdateJourneyRequest = {
  name?: string
  /** Omit to keep; null to clear. */
  date?: string | null
  plan?: JourneyPlan
  /** Replaces annotations; omit to keep surviving point IDs, [] to clear all. */
  annotations?: PointAnnotation[]
}
