import { useEffect, useRef, useState } from 'react'
import { MapPin } from 'lucide-react'
import { createPlanMap, type PlanMap, type PlanMarker } from '../googleMaps'
import styles from './JourneyPlanMap.module.css'

export function JourneyPlanMap({
  points,
  ready,
  error,
}: {
  points: PlanMarker[]
  ready: boolean
  error: string | null
}) {
  const container = useRef<HTMLDivElement>(null)
  const [map, setMap] = useState<PlanMap | null>(null)
  const [mapError, setMapError] = useState(false)
  const lastPoints = useRef('')
  useEffect(() => {
    if (!ready || !container.current) return
    const controller = new AbortController()
    let instance: PlanMap | undefined
    void createPlanMap(
      container.current,
      () => setMapError(true),
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
        if (!controller.signal.aborted) setMapError(true)
      })
    return () => {
      controller.abort()
      instance?.dispose()
      lastPoints.current = ''
    }
  }, [ready])
  useEffect(() => {
    const signature = JSON.stringify(points)
    if (
      ready &&
      !error &&
      !mapError &&
      map &&
      signature !== lastPoints.current
    ) {
      map.update(points)
      lastPoints.current = signature
    }
  }, [points, map, ready, error, mapError])
  const unavailable = Boolean(error) || mapError
  return (
    <section className={styles.panel} aria-label="Selected places map">
      <div ref={container} className={styles.map} inert={unavailable} />
      {(!map || unavailable) && (
        <div className={styles.status} role="status">
          <MapPin aria-hidden="true" />
          <p>{unavailable ? 'Map unavailable' : 'Loading map…'}</p>
          <span>
            {unavailable
              ? 'Maps cannot load right now. Your planning inputs are kept.'
              : 'Your selected places will appear here.'}
          </span>
        </div>
      )}
    </section>
  )
}
