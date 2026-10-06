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
import { CardActions, type CardAction } from './CardActions'
import styles from './SavedJourneys.module.css'

export function JourneyCard({
  journey,
  busy,
  error,
  onOpen,
  onAction,
}: {
  journey: JourneySummary
  busy: boolean
  error?: string
  onOpen: () => void
  onAction: (action: CardAction) => void
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
        {busy && (
          <p role="status" className={styles.cardStatus}>
            Duplicating journey…
          </p>
        )}
        {error && (
          <p role="alert" className={styles.error}>
            {error}
          </p>
        )}
      </div>
      <CardActions name={journey.name} disabled={busy} onAction={onAction} />
    </article>
  )
}
