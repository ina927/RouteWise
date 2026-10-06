import { useEffect, useRef, useState } from 'react'
import { MapPin } from 'lucide-react'
import type { RouteResult } from '../../../contracts/routes'
import {
  createRouteMap,
  onGoogleFailure,
  type RouteMap,
} from '../../journey-planning/googleMaps'
import styles from './OptimisedRouteMap.module.css'

export function OptimisedRouteMap({
  route,
  hoveredSegmentId,
  selectedSegmentId,
}: {
  route: RouteResult
  hoveredSegmentId: string | null
  selectedSegmentId: string | null
}) {
  const container = useRef<HTMLDivElement>(null)
  const [map, setMap] = useState<RouteMap | null>(null)
  const [unavailable, setUnavailable] = useState(false)
  useEffect(() => {
    if (!container.current) return
    const controller = new AbortController()
    let instance: RouteMap | undefined
    const unsubscribe = onGoogleFailure(() => setUnavailable(true))
    void createRouteMap(
      container.current,
      () => setUnavailable(true),
      controller.signal,
    )
      .then((created) => {
        if (controller.signal.aborted) {
          created.dispose()
          return
        }
        instance = created
        setMap(created)
      })
      .catch(() => {
        if (!controller.signal.aborted) setUnavailable(true)
      })
    return () => {
      controller.abort()
      unsubscribe()
      instance?.dispose()
    }
  }, [])
  useEffect(() => {
    if (map && !unavailable) map.update(route)
  }, [map, unavailable, route])
  useEffect(() => {
    if (map && !unavailable) map.highlight(hoveredSegmentId, selectedSegmentId)
  }, [map, unavailable, route, hoveredSegmentId, selectedSegmentId])
  return (
    <section className={styles.panel} aria-label="Route map">
      <div ref={container} className={styles.map} inert={unavailable} />
      {(!map || unavailable) && (
        <div className={styles.status} role="status">
          <MapPin aria-hidden="true" />
          <p>{unavailable ? 'Map unavailable' : 'Loading map…'}</p>
          <span>
            {unavailable
              ? 'Maps cannot load right now. You can still review and edit your route.'
              : 'Your route will appear here.'}
          </span>
        </div>
      )}
    </section>
  )
}
