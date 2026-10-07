import type { JourneyPlan, PointAnnotation } from '../contracts/journeys.ts'
import type { Coordinates, RoutePoint } from '../contracts/places.ts'
import type {
  CalculateRouteRequest,
  OptimizeRouteRequest,
  RouteResult,
} from '../contracts/routes.ts'
import { MockError } from './http.ts'

export function requireValid(
  condition: unknown,
  message: string,
): asserts condition {
  if (!condition) throw new MockError(400, message)
}

export function object(value: unknown): Record<string, unknown> {
  requireValid(
    typeof value === 'object' && value !== null && !Array.isArray(value),
    'Expected a JSON object.',
  )
  return value as Record<string, unknown>
}

function array(value: unknown): unknown[] {
  requireValid(Array.isArray(value), 'Expected a JSON array.')
  return value as unknown[]
}

export function text(value: unknown, label: string): string {
  requireValid(
    typeof value === 'string' && value.trim().length > 0,
    `${label} is required.`,
  )
  return value.trim()
}

export function oneOf<T extends string>(
  value: unknown,
  allowed: readonly T[],
  label: string,
): T {
  requireValid(
    typeof value === 'string' && allowed.includes(value as T),
    `Invalid ${label}.`,
  )
  return value as T
}

function nonNegative(value: unknown): number {
  requireValid(
    typeof value === 'number' && Number.isFinite(value) && value >= 0,
    'Metrics must be finite non-negative numbers.',
  )
  return value
}

function coordinates(value: unknown): Coordinates {
  const point = object(value)
  requireValid(
    typeof point.lat === 'number' &&
      Number.isFinite(point.lat) &&
      Math.abs(point.lat) <= 90,
    'Invalid latitude.',
  )
  requireValid(
    typeof point.lng === 'number' &&
      Number.isFinite(point.lng) &&
      Math.abs(point.lng) <= 180,
    'Invalid longitude.',
  )
  return { lat: point.lat, lng: point.lng }
}

function routePoint(value: unknown): RoutePoint {
  const point = object(value)
  const place = object(point.place)
  requireValid(
    place.address === undefined || typeof place.address === 'string',
    'Address must be text.',
  )
  return {
    id: text(point.id, 'Occurrence ID'),
    place: {
      placeId: text(place.placeId, 'Google place ID'),
      name: text(place.name, 'Place name'),
      ...(place.address === undefined ? {} : { address: place.address }),
      coordinates: coordinates(place.coordinates),
    },
  }
}

function checkPoints(points: RoutePoint[]) {
  requireValid(
    points.length >= 2,
    'A route needs a start and a final occurrence.',
  )
  requireValid(
    points.length <= 10,
    'A journey may contain at most 10 total route locations, including endpoints.',
  )
  requireValid(
    new Set(points.map((point) => point.id)).size === points.length,
    'Route occurrence IDs must be distinct.',
  )
}

const modes = ['walking', 'driving'] as const
const objectives = ['distance', 'time'] as const
const ends = ['open', 'returnToStart', 'fixed'] as const

export function parseOptimize(value: unknown): OptimizeRouteRequest {
  const body = object(value)
  const common = {
    start: routePoint(body.start),
    destinations: array(body.destinations).map(routePoint),
    travelMode: oneOf(body.travelMode, modes, 'travel mode'),
    objective: oneOf(body.objective, objectives, 'optimisation objective'),
  }
  const endMode = oneOf(body.endMode, ends, 'end mode')
  let request: OptimizeRouteRequest
  if (endMode === 'fixed') {
    requireValid(body.end !== undefined, 'Fixed End requires an end location.')
    request = { ...common, endMode, end: routePoint(body.end) }
    checkPoints([common.start, ...common.destinations, request.end])
  } else if (endMode === 'returnToStart') {
    request = {
      ...common,
      endMode,
      returnPointId: text(body.returnPointId, 'Return occurrence ID'),
    }
    checkPoints([
      common.start,
      ...common.destinations,
      { id: request.returnPointId, place: common.start.place },
    ])
  } else {
    request = { ...common, endMode }
    checkPoints([common.start, ...common.destinations])
  }
  return request
}

export function parseCalculate(value: unknown): CalculateRouteRequest {
  const body = object(value)
  const points = array(body.points).map(routePoint)
  checkPoints(points)
  return { points, travelMode: oneOf(body.travelMode, modes, 'travel mode') }
}

function parseRoute(value: unknown): RouteResult {
  const route = object(value)
  const points = array(route.points).map(routePoint)
  checkPoints(points)
  const segments = array(route.segments).map((value, index) => {
    const segment = object(value)
    const fromPointId = text(segment.fromPointId, 'Segment start occurrence')
    const toPointId = text(segment.toPointId, 'Segment end occurrence')
    requireValid(
      fromPointId === points[index]?.id && toPointId === points[index + 1]?.id,
      'Segments must connect consecutive route occurrences.',
    )
    const path = array(segment.path).map(coordinates)
    requireValid(
      path.length >= 2,
      'Segment geometry needs at least two coordinates.',
    )
    return {
      id: text(segment.id, 'Segment ID'),
      fromPointId,
      toPointId,
      distanceMeters: nonNegative(segment.distanceMeters),
      durationSeconds: nonNegative(segment.durationSeconds),
      path,
    }
  })
  requireValid(
    segments.length === points.length - 1,
    'Every consecutive pair requires a segment.',
  )
  requireValid(
    new Set(segments.map((segment) => segment.id)).size === segments.length,
    'Segment IDs must be distinct.',
  )
  const totalDistanceMeters = nonNegative(route.totalDistanceMeters)
  const totalDurationSeconds = nonNegative(route.totalDurationSeconds)
  requireValid(
    Math.abs(
      totalDistanceMeters -
        segments.reduce((sum, segment) => sum + segment.distanceMeters, 0),
    ) < 0.001,
    'Distance total must match the segments.',
  )
  requireValid(
    Math.abs(
      totalDurationSeconds -
        segments.reduce((sum, segment) => sum + segment.durationSeconds, 0),
    ) < 0.001,
    'Duration total must match the segments.',
  )
  return { points, segments, totalDistanceMeters, totalDurationSeconds }
}

export function parsePlan(value: unknown): JourneyPlan {
  const plan = object(value)
  const result = {
    travelMode: oneOf(plan.travelMode, modes, 'travel mode'),
    objective: oneOf(plan.objective, objectives, 'optimisation objective'),
    endMode: oneOf(plan.endMode, ends, 'end mode'),
    route: parseRoute(plan.route),
  }
  if (result.endMode === 'returnToStart') {
    requireValid(
      result.route.points[0]?.place.placeId ===
        result.route.points.at(-1)?.place.placeId,
      'Return to Start must end at the starting place.',
    )
  }
  return result
}

export function parseDate(value: unknown): string {
  const date = text(value, 'Journey date')
  requireValid(
    /^\d{4}-\d{2}-\d{2}$/.test(date),
    'Journey date must be YYYY-MM-DD.',
  )
  const year = Number(date.slice(0, 4))
  const month = Number(date.slice(5, 7))
  const day = Number(date.slice(8, 10))
  const leap = year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0)
  const days = [31, leap ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31]
  requireValid(
    month >= 1 && month <= 12 && day >= 1 && day <= (days[month - 1] ?? 0),
    'Journey date must be a valid calendar date.',
  )
  return date
}

export function parseAnnotations(
  value: unknown,
  points: RoutePoint[],
): PointAnnotation[] {
  const annotations = array(value).map((value) => {
    const annotation = object(value)
    const pointId = text(annotation.pointId, 'Annotated occurrence ID')
    requireValid(
      points.some((point) => point.id === pointId),
      'Annotations must refer to an occurrence in this route.',
    )
    requireValid(
      annotation.note === undefined || typeof annotation.note === 'string',
      'Note must be text.',
    )
    requireValid(
      annotation.plannedTime === undefined ||
        (typeof annotation.plannedTime === 'string' &&
          /^([01]\d|2[0-3]):[0-5]\d$/.test(annotation.plannedTime)),
      'Planned time must be a manually entered HH:mm value.',
    )
    return {
      pointId,
      ...(annotation.note === undefined ? {} : { note: annotation.note }),
      ...(annotation.plannedTime === undefined
        ? {}
        : { plannedTime: annotation.plannedTime }),
    }
  })
  requireValid(
    new Set(annotations.map((item) => item.pointId)).size ===
      annotations.length,
    'Only one annotation entry per occurrence is allowed.',
  )
  return annotations
}
