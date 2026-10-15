import { Badge } from '../../components/ui/Badge'
import type { InvitationStatus } from '../../contracts/sharing'

export function InvitationBadge({ status }: { status: InvitationStatus }) {
  return (
    <Badge
      tone={
        status === 'accepted'
          ? 'success'
          : status === 'pending'
            ? 'info'
            : 'neutral'
      }
    >
      {status === 'accepted'
        ? 'Accepted'
        : status === 'pending'
          ? 'Pending'
          : status === 'removed'
            ? 'Removed'
            : 'Declined'}
    </Badge>
  )
}
