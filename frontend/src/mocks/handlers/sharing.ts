import { http, HttpResponse } from 'msw/http'
import { apiUrl } from '../../api/config.ts'
import { readJson, respond } from '../http.ts'
import {
  listIncomingInvitations,
  listJourneyInvitations,
  listSharedJourneys,
  removeMockSharedJourney,
  respondMockInvitation,
  searchMockUsers,
  sendMockInvitation,
} from '../store.ts'
import { object, oneOf, text } from '../validation.ts'

export const sharingHandlers = [
  http.get(apiUrl('/users'), ({ request }) =>
    HttpResponse.json(
      searchMockUsers(new URL(request.url).searchParams.get('query') ?? ''),
    ),
  ),
  http.get(apiUrl('/journeys/:id/invitations'), ({ params }) =>
    respond(() =>
      HttpResponse.json(listJourneyInvitations(text(params.id, 'Journey ID'))),
    ),
  ),
  http.post(apiUrl('/journeys/:id/invitations'), ({ params, request }) =>
    respond(async () => {
      const body = object(await readJson(request))
      return HttpResponse.json(
        sendMockInvitation(
          text(params.id, 'Journey ID'),
          text(body.recipientId, 'Recipient ID'),
        ),
        { status: 201 },
      )
    }),
  ),
  http.get(apiUrl('/invitations'), () =>
    HttpResponse.json(listIncomingInvitations()),
  ),
  http.patch(apiUrl('/invitations/:id'), ({ params, request }) =>
    respond(async () => {
      const body = object(await readJson(request))
      return HttpResponse.json(
        respondMockInvitation(
          text(params.id, 'Invitation ID'),
          oneOf(body.status, ['accepted', 'declined'], 'invitation response'),
        ),
      )
    }),
  ),
  http.get(apiUrl('/shared-journeys'), () =>
    HttpResponse.json(listSharedJourneys()),
  ),
  http.delete(apiUrl('/shared-journeys/:id'), ({ params }) =>
    respond(() => {
      removeMockSharedJourney(text(params.id, 'Journey ID'))
      return new HttpResponse(null, { status: 204 })
    }),
  ),
]
