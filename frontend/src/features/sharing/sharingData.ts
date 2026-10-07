import { queryOptions, type QueryClient } from '@tanstack/react-query'
import {
  getIncomingInvitations,
  getJourneyInvitations,
  getSharedJourneys,
} from '../../api/sharingApi'
import type { Invitation } from '../../contracts/sharing'
import type { JourneySummary } from '../../contracts/journeys'
import { journeyKey } from '../journey-details/journeyData'

export const incomingInvitationsKey = ['incoming-invitations'] as const
export const sharedJourneysKey = ['shared-journeys'] as const
export const journeyInvitationsKey = (id: string) =>
  ['journey-invitations', id] as const
export const incomingInvitationsOptions = queryOptions({
  queryKey: incomingInvitationsKey,
  queryFn: ({ signal }) => getIncomingInvitations(signal),
  staleTime: 30_000,
  retry: false,
})
export const sharedJourneysOptions = queryOptions({
  queryKey: sharedJourneysKey,
  queryFn: ({ signal }) => getSharedJourneys(signal),
  retry: false,
})
export const journeyInvitationsOptions = (id: string) =>
  queryOptions({
    queryKey: journeyInvitationsKey(id),
    queryFn: ({ signal }) => getJourneyInvitations(id, signal),
    retry: false,
  })

export async function cacheInvitationResponse(
  client: QueryClient,
  invitation: Invitation,
) {
  await Promise.all([
    client.cancelQueries({ queryKey: incomingInvitationsKey }),
    ...(invitation.status === 'accepted'
      ? [
          client.cancelQueries({ queryKey: sharedJourneysKey }),
          client.cancelQueries({ queryKey: journeyKey(invitation.journey.id) }),
        ]
      : []),
  ])
  client.setQueryData<Invitation[]>(incomingInvitationsKey, (current) =>
    current?.map((item) => (item.id === invitation.id ? invitation : item)),
  )
  if (invitation.status === 'accepted') {
    client.setQueryData<JourneySummary[]>(sharedJourneysKey, (current) =>
      current
        ? [
            ...current.filter((item) => item.id !== invitation.journey.id),
            invitation.journey,
          ]
        : undefined,
    )
    // A previous denied detail query must be allowed to load after acceptance.
    client.removeQueries({ queryKey: journeyKey(invitation.journey.id) })
  }
  await Promise.all([
    client.invalidateQueries({ queryKey: incomingInvitationsKey }),
    ...(invitation.status === 'accepted'
      ? [client.invalidateQueries({ queryKey: sharedJourneysKey })]
      : []),
  ])
}
