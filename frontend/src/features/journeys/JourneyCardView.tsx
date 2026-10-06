import type { ReactNode } from 'react'
import {
  ArrowRight,
  CalendarDays,
  Car,
  Clock,
  Footprints,
  MapPin,
  Route,
} from 'lucide-react'
import type { JourneySummary } from '../../contracts/journeys'
import { PlacePhoto } from '../journey-details/PlacePhoto'
import {
  formatDistance,
  formatDuration,
} from '../route-optimisation/formatters'
import styles from './JourneyCards.module.css'

export function JourneyCardView({
  journey,
  children,
  actions,
  sharedBy,
  onOpen,
}: {
  journey: JourneySummary
  children?: ReactNode
  actions: ReactNode
  sharedBy?: string
  onOpen: () => void
}) {
  const Transport = journey.travelMode === 'walking' ? Footprints : Car
  return (
    <article className={styles.card}>
      <PlacePhoto
        key={journey.representativePlaceId}
        cover
        place={{ placeId: journey.representativePlaceId, name: journey.name }}
      />
      <div className={styles.cardBody}>
        <h2>
          <button
            type="button"
            className={styles.open}
            onClick={onOpen}
            aria-label={`Open ${journey.name}`}
          >
            {journey.name}
          </button>
        </h2>
        {sharedBy && <p className={styles.status}>Shared by {sharedBy}</p>}
        {journey.date && (
          <p className={styles.date}>
            <CalendarDays aria-hidden="true" />
            <time dateTime={journey.date}>{journey.date}</time>
          </p>
        )}
        <p className={styles.endpoints}>
          <MapPin aria-hidden="true" />
          <span>{journey.start.name}</span>
          <ArrowRight aria-label="to" />
          <span>{journey.end.name}</span>
        </p>
        <div className={styles.metrics}>
          <span>
            <Transport aria-hidden="true" />
            {journey.travelMode === 'walking' ? 'Walking' : 'Driving'}
          </span>
          <span>{journey.locationCount} locations</span>
          <span>
            <Route aria-hidden="true" />
            {formatDistance(journey.totalDistanceMeters)}
          </span>
          <span>
            <Clock aria-hidden="true" />
            {formatDuration(journey.totalDurationSeconds)}
          </span>
        </div>
        {children}
      </div>
      {actions}
    </article>
  )
}
