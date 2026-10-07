import { useEffect, useRef, useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { calculateRoute, optimizeRoute } from '../../api/routesApi'
import type { RoutePoint } from '../../contracts/places'
import type {
  OptimizeRouteRequest,
  TravelMode,
  OptimisationObjective,
} from '../../contracts/routes'
import type { PlanningResult } from '../journey-planning/planning'
import { usePlanning } from '../journey-planning/planningContext'

type Settings = { travelMode: TravelMode; objective: OptimisationObjective }
function settingsOf(request: OptimizeRouteRequest): Settings {
  return { travelMode: request.travelMode, objective: request.objective }
}
type Operation =
  | { kind: 'optimise'; request: OptimizeRouteRequest; signal: AbortSignal }
  | {
      kind: 'calculate'
      points: RoutePoint[]
      travelMode: TravelMode
      signal: AbortSignal
    }

export function useRouteUpdates(result: PlanningResult) {
  const { setResult, setDraft } = usePlanning()
  const [settings, setSettings] = useState<Settings>(() =>
    settingsOf(result.request),
  )
  const desired = useRef<Settings>(settingsOf(result.request))
  const active = useRef<AbortController | null>(null)
  const [pending, setPending] = useState<Operation['kind'] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const mutation = useMutation({
    mutationFn: (operation: Operation) =>
      operation.kind === 'optimise'
        ? optimizeRoute(operation.request, operation.signal)
        : calculateRoute(
            { points: operation.points, travelMode: operation.travelMode },
            operation.signal,
          ),
    retry: false,
  })
  useEffect(
    () => () => {
      active.current?.abort()
    },
    [],
  )

  async function changeSettings(change: Partial<Settings>) {
    if (pending === 'calculate') return
    const next = { ...desired.current, ...change }
    desired.current = next
    setSettings(next)
    active.current?.abort()
    const controller = new AbortController()
    active.current = controller
    setPending('optimise')
    setError(null)
    // The submitted planning problem is independent of any manual display order.
    const request = structuredClone({ ...result.request, ...next })
    try {
      const route = await mutation.mutateAsync({
        kind: 'optimise',
        request,
        signal: controller.signal,
      })
      if (controller.signal.aborted || active.current !== controller) return
      setResult({
        request,
        optimisedRoute: structuredClone(route),
        currentRoute: route,
        modified: false,
      })
      setDraft((current) => ({ ...current, ...next }))
    } catch {
      if (controller.signal.aborted || active.current !== controller) return
      desired.current = settingsOf(result.request)
      setSettings(settingsOf(result.request))
      setError(
        'Unable to update the route. Your last successful route and settings are kept.',
      )
    } finally {
      if (active.current === controller) {
        active.current = null
        setPending(null)
      }
    }
  }

  async function reorder(points: RoutePoint[]) {
    if (active.current) return
    const controller = new AbortController()
    active.current = controller
    setPending('calculate')
    setError(null)
    try {
      // Exact-order calculation never replaces the latest optimisation baseline.
      const route = await mutation.mutateAsync({
        kind: 'calculate',
        points,
        travelMode: result.request.travelMode,
        signal: controller.signal,
      })
      if (controller.signal.aborted || active.current !== controller) return
      setResult((current) =>
        current ? { ...current, currentRoute: route, modified: true } : current,
      )
    } catch {
      if (!controller.signal.aborted && active.current === controller)
        setError('Unable to calculate that order. Your previous route is kept.')
    } finally {
      if (active.current === controller) {
        active.current = null
        setPending(null)
      }
    }
  }

  function reset() {
    if (active.current) return
    setResult((current) =>
      current
        ? {
            ...current,
            currentRoute: structuredClone(current.optimisedRoute),
            modified: false,
          }
        : current,
    )
    setError(null)
  }
  return { settings, pending, error, changeSettings, reorder, reset }
}
