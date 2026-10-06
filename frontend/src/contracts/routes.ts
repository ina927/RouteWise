import type { Coordinates, RoutePoint } from './places.ts'

export type TravelMode = 'walking' | 'driving'
export type OptimisationObjective = 'distance' | 'time'
export type EndMode = 'open' | 'returnToStart' | 'fixed'

export type OptimizeRouteRequest = {
  start: RoutePoint
  destinations: RoutePoint[]
  travelMode: TravelMode
  objective: OptimisationObjective
} & (
  | { endMode: 'open' }
  | { endMode: 'fixed'; end: RoutePoint }
  | { endMode: 'returnToStart'; returnPointId: string }
)

export type CalculateRouteRequest = {
  points: RoutePoint[]
  travelMode: TravelMode
}

export type RouteSegment = {
  id: string
  fromPointId: string
  toPointId: string
  distanceMeters: number
  durationSeconds: number
  path: Coordinates[]
}

export type RouteResult = {
  points: RoutePoint[]
  segments: RouteSegment[]
  totalDistanceMeters: number
  totalDurationSeconds: number
}
