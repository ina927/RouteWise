import type { RoutePoint } from '../contracts/places.ts'
import type {
  OptimizeRouteRequest,
  RouteResult,
  TravelMode,
} from '../contracts/routes.ts'

// Development-only costs and straight-line geometry. These are not road data.
export function fixtureRoute(
  points: RoutePoint[],
  travelMode: TravelMode,
): RouteResult {
  const segments = points.slice(1).map((to, index) => {
    const from = points[index]
    if (!from) throw new Error('Missing fixture route point.')
    let seed = 0
    for (const character of `${from.place.placeId}>${to.place.placeId}`) {
      seed = (seed * 31 + character.charCodeAt(0)) % 10000
    }
    const distanceMeters = 1000 + (seed % 2500)
    return {
      id: `segment:${encodeURIComponent(from.id)}:${encodeURIComponent(to.id)}`,
      fromPointId: from.id,
      toPointId: to.id,
      distanceMeters,
      durationSeconds: Math.round(
        distanceMeters / (travelMode === 'walking' ? 1.3 : 9),
      ),
      path: [from.place.coordinates, to.place.coordinates],
    }
  })
  return structuredClone({
    points,
    segments,
    totalDistanceMeters: segments.reduce(
      (sum, item) => sum + item.distanceMeters,
      0,
    ),
    totalDurationSeconds: segments.reduce(
      (sum, item) => sum + item.durationSeconds,
      0,
    ),
  })
}

export function fixtureOptimisation(
  request: OptimizeRouteRequest,
): RouteResult {
  // Only a predictable UI exercise: never rank routes or approximate the algorithm.
  const destinations = [...request.destinations]
  if (request.objective === 'distance') destinations.reverse()
  if (request.travelMode === 'walking' && destinations.length > 1) {
    const first = destinations.shift()
    if (first) destinations.push(first)
  }
  const points = [request.start, ...destinations]
  if (request.endMode === 'fixed') points.push(request.end)
  if (request.endMode === 'returnToStart') {
    points.push({ id: request.returnPointId, place: request.start.place })
  }
  return fixtureRoute(points, request.travelMode)
}
