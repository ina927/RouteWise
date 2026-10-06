import type { JourneySummary } from '../contracts/journeys.ts'
import type {
  Invitation,
  RespondToInvitationRequest,
  SendInvitationRequest,
} from '../contracts/sharing.ts'
import { request } from './httpClient.ts'

export function getJourneyInvitations(journeyId: string, signal?: AbortSignal) {
  return request<Invitation[]>(
    `/journeys/${encodeURIComponent(journeyId)}/invitations`,
    { signal },
  )
}

export function sendInvitation(journeyId: string, body: SendInvitationRequest) {
  return request<Invitation>(
    `/journeys/${encodeURIComponent(journeyId)}/invitations`,
    { method: 'POST', body },
  )
}

export function getIncomingInvitations(signal?: AbortSignal) {
  return request<Invitation[]>('/invitations', { signal })
}

export function respondToInvitation(
  id: string,
  body: RespondToInvitationRequest,
) {
  return request<Invitation>(`/invitations/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body,
  })
}

export function getSharedJourneys(signal?: AbortSignal) {
  return request<JourneySummary[]>('/shared-journeys', { signal })
}

export function removeSharedJourney(journeyId: string) {
  return request(`/shared-journeys/${encodeURIComponent(journeyId)}`, {
    method: 'DELETE',
    noContent: true,
  })
}
