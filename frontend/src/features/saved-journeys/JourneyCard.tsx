import type { JourneySummary } from '../../contracts/journeys'
import { JourneyCardView } from '../journeys/JourneyCardView'
import { JourneySharingStatus } from '../sharing/JourneySharingStatus'
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
  return (
    <JourneyCardView
      journey={journey}
      onOpen={onOpen}
      actions={
        <CardActions name={journey.name} disabled={busy} onAction={onAction} />
      }
    >
      <JourneySharingStatus journeyId={journey.id} />
      {busy && (
        <p role="status" className={styles.cardStatus}>
          Duplicating journey?
        </p>
      )}
      {error && (
        <p role="alert" className={styles.error}>
          {error}
        </p>
      )}
    </JourneyCardView>
  )
}
