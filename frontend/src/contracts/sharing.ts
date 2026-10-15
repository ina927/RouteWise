import type { JourneySummary } from './journeys.ts'
import type { UserSummary } from './users.ts'

export type InvitationStatus = 'pending' | 'accepted' | 'declined' | 'removed'

export type Invitation = {
  id: string
  journey: JourneySummary
  sender: UserSummary
  recipient: UserSummary
  status: InvitationStatus
}

export type SendInvitationRequest = { recipientId: string }
export type RespondToInvitationRequest = { status: 'accepted' | 'declined' }
