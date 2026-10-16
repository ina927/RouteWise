import {
  ArrowRight,
  CalendarDays,
  Car,
  Clock,
  Footprints,
  LogOut,
  MapPin,
  Route,
} from 'lucide-react'
import type { JourneySummary } from '../../contracts/journeys'
import { JourneyCardMenu } from '../journeys/JourneyCardMenu'
import {
  formatDistance,
  formatDuration,
} from '../route-optimisation/formatters'
import styles from './Sharing.module.css'
import savedStyles from '../saved-journeys/SavedJourneys.module.css'

const sharedActions = [
  {
    id: 'remove',
    label: 'Remove from Shared',
    icon: LogOut,
    destructive: true,
  },
] as const

type Props = {
  journey: JourneySummary
  onOpen: () => void
  onRemove: () => void
}

export function SharedJourneyListRow({
  journey,
  onOpen,
  onRemove,
}: Props) {
  const Transport = journey.travelMode === 'walking' ? Footprints : Car

  return (
    <article className={savedStyles.listRow}>
      <div className={savedStyles.listContent}>
        <h2 className={savedStyles.listTitle}>
          <button type="button" onClick={onOpen}>
            {journey.name}
          </button>
        </h2>

        <div className={savedStyles.listEndpoints}>
          <MapPin aria-hidden="true" />
          <span>{journey.start.name}</span>
          <ArrowRight aria-label="to" />
          <span>{journey.end.name}</span>
        </div>

        <div className={savedStyles.listMetrics}>
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

        <p className={styles.sharedOwner}>
          Shared by {journey.owner.username}
        </p>
      </div>

      <div className={savedStyles.listActions}>
        <JourneyCardMenu
          name={journey.name}
          disabled={false}
          actions={sharedActions}
          onAction={onRemove}
        />
      </div>
    </article>
  )
}