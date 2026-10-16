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
import {
  formatDistance,
  formatDuration,
} from '../route-optimisation/formatters'
import { JourneySharingStatus } from '../sharing/JourneySharingStatus'
import { CardActions, type CardAction } from './CardActions'
import styles from './SavedJourneys.module.css'

type Props = {
  journey: JourneySummary
  busy: boolean
  error?: string
  onOpen: () => void
  onAction: (action: CardAction) => void
}

export function JourneyListRow({
  journey,
  busy,
  error,
  onOpen,
  onAction,
}: Props) {
  const Transport = journey.travelMode === 'walking' ? Footprints : Car

  return (
    <article className={styles.listRow}>
      <div className={styles.listContent}>
        <h2 className={styles.listTitle}>
          <button type="button" onClick={onOpen}>
            {journey.name}
          </button>
        </h2>

        <div className={styles.listEndpoints}>
          <MapPin aria-hidden="true" />
          <span>{journey.start.name}</span>
          <ArrowRight aria-label="to" />
          <span>{journey.end.name}</span>
        </div>

        <div className={styles.listMetrics}>
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
          {journey.date && (
            <span>
              <CalendarDays aria-hidden="true" />
              <time dateTime={journey.date}>{journey.date}</time>
            </span>
          )}
        </div>

        <JourneySharingStatus journeyId={journey.id} />

        {busy && <p role="status">Duplicating journey…</p>}
        {error && (
          <p role="alert" className={styles.error}>
            {error}
          </p>
        )}
      </div>

      <div className={styles.listActions}>
        <CardActions
          name={journey.name}
          disabled={busy}
          onAction={onAction}
        />
      </div>
    </article>
  )
}