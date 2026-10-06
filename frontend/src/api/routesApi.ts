import type {
  CalculateRouteRequest,
  OptimizeRouteRequest,
  RouteResult,
} from '../contracts/routes.ts'
import { request } from './httpClient.ts'

export function optimizeRoute(
  body: OptimizeRouteRequest,
  signal?: AbortSignal,
) {
  return request<RouteResult>('/routes/optimize', {
    method: 'POST',
    body,
    signal,
  })
}

export function calculateRoute(
  body: CalculateRouteRequest,
  signal?: AbortSignal,
) {
  return request<RouteResult>('/routes/calculate', {
    method: 'POST',
    body,
    signal,
  })
}
