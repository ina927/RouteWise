import { useQuery } from '@tanstack/react-query'
import { journeyInvitationsOptions } from './sharingData'
import styles from './Sharing.module.css'

export function JourneySharingStatus({ journeyId }: { journeyId: string }) {
  const invitations = useQuery(journeyInvitationsOptions(journeyId))
  if (invitations.isPending)
    return (
      <p className={styles.secondary} role="status">
        Loading sharing status…
      </p>
    )
  if (invitations.isError)
    return <p className={styles.secondary}>Sharing status unavailable.</p>

  const accepted = invitations.data.filter((item) => item.status === 'accepted')
  const pending = invitations.data.filter((item) => item.status === 'pending')
  const recipients = (items: typeof accepted) =>
    `${items[0]?.recipient.username ?? ''}${items.length > 1 ? ` and ${items.length - 1} other${items.length === 2 ? '' : 's'}` : ''}`
  return (
    <div className={styles.secondary} aria-live="polite">
      {accepted.length > 0 && <p>Shared with {recipients(accepted)}</p>}
      {pending.length > 0 && <p>Invitation pending: {recipients(pending)}</p>}
      {!accepted.length && !pending.length && <p>Not shared</p>}
    </div>
  )
}
