import { useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router'
import { LayoutGrid, List, LogOut } from 'lucide-react'
import { respondToInvitation } from '../../api/sharingApi'
import type { Invitation } from '../../contracts/sharing'
import type { JourneySummary } from '../../contracts/journeys'
import { ProductShell } from '../../app/ProductShell'
import { PageHeader } from '../../components/layout/PageHeader'
import { Button } from '../../components/ui/Button'
import { JourneyCardView } from '../journeys/JourneyCardView'
import { JourneyCardMenu } from '../journeys/JourneyCardMenu'
import {
  formatDistance,
  formatDuration,
} from '../route-optimisation/formatters'
import {
  cacheInvitationResponse,
  incomingInvitationsOptions,
  sharedJourneysOptions,
} from './sharingData'
import { RemoveSharedJourneyModal } from './RemoveSharedJourneyModal'
import { InitialAvatar } from './InitialAvatar'
import { SharedJourneyListRow } from './SharedJourneyListRow'
import styles from './Sharing.module.css'
import savedStyles from '../saved-journeys/SavedJourneys.module.css'
import cardStyles from '../journeys/JourneyCards.module.css'

const sharedActions = [
  {
    id: 'remove',
    label: 'Remove from Shared',
    icon: LogOut,
    destructive: true,
  },
] as const

type View = 'cards' | 'list'

export function SharedTripsPage() {
  const navigate = useNavigate()
  const incoming = useQuery(incomingInvitationsOptions)
  const shared = useQuery(sharedJourneysOptions)
  const [removing, setRemoving] = useState<JourneySummary | null>(null)
  const [feedback, setFeedback] = useState('')
  const [view, setView] = useState<View>(() =>
    localStorage.getItem('routewise-shared-view') === 'list' ? 'list' : 'cards',
  )
  const pendingHeading = useRef<HTMLHeadingElement>(null)
  const sharedHeading = useRef<HTMLHeadingElement>(null)
  const pending =
    incoming.data?.filter((item) => item.status === 'pending') ?? []

  function changeView(next: View) {
    setView(next)
    localStorage.setItem('routewise-shared-view', next)
  }

  return (
    <ProductShell activeItem="shared">
      <PageHeader
        title="Shared trips"
        subtitle="Trips shared with you by other RouteWise users."
      />
      <section
        aria-labelledby="pending-heading"
        className={`${styles.section} ${styles.pending}`}
      >
        <h2 id="pending-heading" ref={pendingHeading} tabIndex={-1}>
          Pending invitations
        </h2>
        {incoming.isPending ? (
          <p role="status">Loading invitations…</p>
        ) : incoming.isError ? (
          <div>
            <p role="alert" className={styles.error}>
              {incoming.error.message}
            </p>
            <Button
              variant="secondary"
              onClick={() => {
                void incoming.refetch()
              }}
            >
              Retry invitations
            </Button>
          </div>
        ) : pending.length ? (
          <ul className={styles.list}>
            {pending.map((invitation) => (
              <PendingInvitation
                key={invitation.id}
                invitation={invitation}
                onResponded={(status) => {
                  setFeedback(
                    status === 'accepted'
                      ? 'Invitation accepted. Journey added to Shared with me.'
                      : 'Invitation declined.',
                  )
                  pendingHeading.current?.focus()
                }}
              />
            ))}
          </ul>
        ) : (
          <p className={styles.secondary}>No pending invitations.</p>
        )}
      </section>
      <section aria-labelledby="shared-heading" className={styles.section}>
        <div className={styles.sharedHeadingRow}>
          <h2 id="shared-heading" ref={sharedHeading} tabIndex={-1}>
            Shared with me
          </h2>
          <div
            className={savedStyles.viewToggle}
            role="group"
            aria-label="Shared journey view"
          >
            <button
              type="button"
              className={`${savedStyles.viewButton} ${view === 'cards' ? savedStyles.activeView : ''}`}
              aria-label="Card view"
              aria-pressed={view === 'cards'}
              onClick={() => changeView('cards')}
            >
              <LayoutGrid aria-hidden="true" />
            </button>
            <button
              type="button"
              className={`${savedStyles.viewButton} ${view === 'list' ? savedStyles.activeView : ''}`}
              aria-label="List view"
              aria-pressed={view === 'list'}
              onClick={() => changeView('list')}
            >
              <List aria-hidden="true" />
            </button>
          </div>
        </div>
        <p role="status" className={styles.feedback}>
          {feedback}
        </p>
        {shared.isPending ? (
          <p role="status">Loading shared journeys…</p>
        ) : shared.isError ? (
          <div>
            <p role="alert" className={styles.error}>
              {shared.error.message}
            </p>
            <Button
              variant="secondary"
              onClick={() => {
                void shared.refetch()
              }}
            >
              Retry shared journeys
            </Button>
          </div>
        ) : shared.data.length ? (
          <div className={view === 'cards' ? cardStyles.grid : savedStyles.list}>
            {shared.data.map((journey) =>
              view === 'cards' ? (
                <JourneyCardView
                  key={journey.id}
                  journey={journey}
                  sharedBy={journey.owner.username}
                  onOpen={() => {
                    void navigate(`/journeys/${encodeURIComponent(journey.id)}`)
                  }}
                  actions={
                    <JourneyCardMenu
                      name={journey.name}
                      disabled={false}
                      actions={sharedActions}
                      onAction={() => setRemoving(journey)}
                    />
                  }
                />
              ) : (
                <SharedJourneyListRow
                  key={journey.id}
                  journey={journey}
                  onOpen={() => {
                    void navigate(`/journeys/${encodeURIComponent(journey.id)}`)
                  }}
                  onRemove={() => setRemoving(journey)}
                />
              ),
            )}
          </div>
        ) : (
          <p className={styles.secondary}>
            No shared journeys yet. Accepted invitations will appear here.
          </p>
        )}
      </section>
      {removing && (
        <RemoveSharedJourneyModal
          journey={removing}
          onClose={() => setRemoving(null)}
          onRemoved={() => {
            setRemoving(null)
            setFeedback('Journey removed from your Shared trips.')
            requestAnimationFrame(() => sharedHeading.current?.focus())
          }}
        />
      )}
    </ProductShell>
  )
}

function PendingInvitation({
  invitation,
  onResponded,
}: {
  invitation: Invitation
  onResponded: (status: 'accepted' | 'declined') => void
}) {
  const client = useQueryClient()
  const guard = useRef(false)
  const mutation = useMutation({
    mutationFn: async (status: 'accepted' | 'declined') => {
      const updated = await respondToInvitation(invitation.id, { status })
      await cacheInvitationResponse(client, updated)
    },
  })
  async function respond(status: 'accepted' | 'declined') {
    if (guard.current) return
    guard.current = true
    try {
      await mutation.mutateAsync(status)
      onResponded(status)
    } catch {
      /* Keep the pending invitation and local error for retry. */
    } finally {
      guard.current = false
    }
  }
  const journey = invitation.journey
  return (
    <li className={styles.row}>
      <InitialAvatar user={invitation.sender} />
      <div className={styles.context}>
        <h3>{journey.name}</h3>
        <p className={styles.secondary}>
          Shared by {invitation.sender.username}
          {journey.date && (
            <>
              {' '}
              · <time dateTime={journey.date}>{journey.date}</time>
            </>
          )}
        </p>
        <p className={styles.secondary}>
          {journey.travelMode === 'walking' ? 'Walking' : 'Driving'} ·{' '}
          {journey.locationCount} locations ·{' '}
          {formatDistance(journey.totalDistanceMeters)} ·{' '}
          {formatDuration(journey.totalDurationSeconds)}
        </p>
        {mutation.isPending && (
          <p role="status" className={styles.secondary}>
            Updating invitation…
          </p>
        )}
        {mutation.error && (
          <p role="alert" className={styles.error}>
            {mutation.error.message}
          </p>
        )}
      </div>
      <div className={styles.actions}>
        <Button
          variant="secondary"
          aria-label={`Decline ${journey.name}`}
          disabled={mutation.isPending}
          onClick={() => {
            void respond('declined')
          }}
        >
          Decline
        </Button>
        <Button
          aria-label={`Accept ${journey.name}`}
          disabled={mutation.isPending}
          onClick={() => {
            void respond('accepted')
          }}
        >
          Accept
        </Button>
      </div>
    </li>
  )
}